using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitAgenticAICompanion.Storage;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class CodexAgentRuntimeClient : IAgentRuntimeClient, IDisposable
    {
        private const string ClientVersion = "0.1.0";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ExecTimeout = TimeSpan.FromMinutes(2);
        private readonly LocalStoragePaths _paths;
        private readonly ProjectThreadStore _threadStore;
        private readonly string _schemaPath;
        private readonly object _schemaGate;
        private readonly SemaphoreSlim _processGate;
        private readonly object _notificationGate;
        private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode>> _pendingRequests;
        private readonly JsonSerializerOptions _jsonOptions;
        private Process _process;
        private StreamWriter _stdin;
        private Task _stdoutPump;
        private Task _stderrPump;
        private long _nextRequestId;
        private bool _isInitialized;
        private string _lastTransportError;
        private Action<string, JsonNode> _notificationHandler;

        public CodexAgentRuntimeClient(LocalStoragePaths paths, ProjectThreadStore threadStore)
        {
            _paths = paths;
            _threadStore = threadStore;
            _schemaPath = Path.Combine(_paths.StatePath, "codex-output-schema.json");
            _schemaGate = new object();
            _processGate = new SemaphoreSlim(1, 1);
            _notificationGate = new object();
            _pendingRequests = new ConcurrentDictionary<long, TaskCompletionSource<JsonNode>>();
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
        }

        public async Task<AgentRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            CodexCliResult result;
            try
            {
                result = await RunCliAsync("login status", null, StatusTimeout, cancellationToken);
            }
            catch (Exception ex)
            {
                return new AgentRuntimeStatus("Codex", false, false, false, true, "Codex CLI unavailable: " + ex.Message);
            }

            var detail = FirstNonEmpty(
                result.StandardOutput.Trim(),
                result.StandardError.Trim(),
                "Codex login status returned no detail.");
            var isAuthenticated = result.ExitCode == 0 &&
                detail.IndexOf("logged in", StringComparison.OrdinalIgnoreCase) >= 0;

            return new AgentRuntimeStatus("Codex", true, isAuthenticated, isAuthenticated, true, detail);
        }

        public Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveCodexExecutable(),
                    Arguments = "login",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = _paths.RootPath,
                },
            };

            if (!process.Start())
            {
                return Task.FromResult(new LoginStartResult(false, string.Empty, "Failed to start Codex login."));
            }

            return Task.FromResult(new LoginStartResult(
                true,
                string.Empty,
                "Codex login started. Complete it in the launched window/browser, then refresh auth."));
        }

        public async Task<ProposalCandidate> CreateProposalAsync(PlanningRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var status = await GetStatusAsync(cancellationToken);
            if (!status.IsAvailable)
            {
                throw new InvalidOperationException(status.Detail);
            }

            if (!status.IsAuthenticated)
            {
                throw new InvalidOperationException("Codex is not signed in. Use Sign in, complete the browser flow, then refresh auth.");
            }

            var prompt = BuildPlanningPrompt(request);
            var structuredJson = await RunPlanningTurnAsync(request.ContextSnapshot, prompt, true, cancellationToken);

            var proposalData = JsonSerializer.Deserialize<CodexPlanningPayload>(structuredJson, _jsonOptions);
            if (proposalData == null)
            {
                throw new InvalidOperationException("Codex returned an empty planning payload.");
            }

            return BuildProposalCandidate(request.Prompt, proposalData, 0);
        }

        public async Task<ProposalCandidate> RepairProposalAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            GeneratedActionCompilationResult compilation,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (failedProposal == null)
            {
                throw new ArgumentNullException(nameof(failedProposal));
            }

            if (compilation == null || compilation.IsSuccess)
            {
                return failedProposal;
            }

            var prompt = BuildRepairPrompt(request, failedProposal, compilation);
            var structuredJson = await RunPlanningTurnAsync(request.ContextSnapshot, prompt, false, cancellationToken);

            var repairedData = JsonSerializer.Deserialize<CodexPlanningPayload>(structuredJson, _jsonOptions);
            if (repairedData == null || !RequiresGeneratedCode(repairedData) || string.IsNullOrWhiteSpace(repairedData.GeneratedSource))
            {
                return failedProposal;
            }

            return BuildProposalCandidate(request.Prompt, repairedData, 1);
        }

        public void Dispose()
        {
        }

        private async Task<string> RunPlanningTurnAsync(
            RevitContextSnapshot snapshot,
            string prompt,
            bool useOutputSchema,
            CancellationToken cancellationToken)
        {
            var projectKey = BuildProjectKey(snapshot);
            var storedThreadId = _threadStore.GetThreadId(projectKey);

            try
            {
                var result = await RunTurnAsync(storedThreadId, prompt, useOutputSchema, cancellationToken);
                if (!string.IsNullOrWhiteSpace(result.ThreadId))
                {
                    _threadStore.SetThreadId(projectKey, result.ThreadId);
                }

                return result.StructuredPayload;
            }
            catch (InvalidOperationException ex) when (!string.IsNullOrWhiteSpace(storedThreadId) && LooksLikeMissingThread(ex.Message))
            {
                _threadStore.ClearThreadId(projectKey);
                var retry = await RunTurnAsync(null, prompt, true, cancellationToken);
                if (!string.IsNullOrWhiteSpace(retry.ThreadId))
                {
                    _threadStore.SetThreadId(projectKey, retry.ThreadId);
                }

                return retry.StructuredPayload;
            }
        }

        private async Task<CodexTurnResult> RunTurnAsync(
            string threadId,
            string prompt,
            bool useOutputSchema,
            CancellationToken cancellationToken)
        {
            var args = new StringBuilder();
            var isResume = !string.IsNullOrWhiteSpace(threadId);
            args.Append("exec ");
            if (isResume)
            {
                args.Append("resume ");
                args.Append(threadId);
                args.Append(' ');
                args.Append("--skip-git-repo-check --json ");
            }
            else
            {
                args.Append("--skip-git-repo-check --sandbox read-only --json ");
                if (useOutputSchema)
                {
                    args.Append("--output-schema ");
                    args.Append('"');
                    args.Append(EnsureOutputSchemaFile());
                    args.Append("\" ");
                }
            }

            args.Append("- ");

            var result = await RunCliAsync(args.ToString(), prompt ?? string.Empty, ExecTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(FirstNonEmpty(
                    result.StandardError.Trim(),
                    result.StandardOutput.Trim(),
                    "Codex exec failed."));
            }

            var turn = ParseTurnOutput(result.StandardOutput);
            if (!string.IsNullOrWhiteSpace(turn.StructuredPayload))
            {
                return turn;
            }

            throw new InvalidOperationException(FirstNonEmpty(
                turn.Error,
                result.StandardError.Trim(),
                "Codex completed without returning a structured payload."));
        }

        private async Task<CodexCliResult> RunCliAsync(
            string arguments,
            string standardInput,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveCodexExecutable(),
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _paths.RootPath,
                },
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start Codex CLI.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!string.IsNullOrEmpty(standardInput))
            {
                try
                {
                    await process.StandardInput.WriteLineAsync(standardInput);
                    await process.StandardInput.FlushAsync();
                }
                catch (IOException)
                {
                    var earlyStdout = await stdoutTask;
                    var earlyStderr = await stderrTask;
                    throw new InvalidOperationException(FirstNonEmpty(
                        earlyStderr.Trim(),
                        earlyStdout.Trim(),
                        "Codex CLI closed stdin before the prompt could be written."));
                }
            }

            process.StandardInput.Close();

            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, CancellationToken.None));
            if (completed != waitTask)
            {
                TryKill(process);
                throw new TimeoutException("Timed out waiting for Codex CLI process.");
            }

            await waitTask;
            return new CodexCliResult(process.ExitCode, await stdoutTask, await stderrTask);
        }

        private string EnsureOutputSchemaFile()
        {
            lock (_schemaGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_schemaPath) ?? _paths.StatePath);
                File.WriteAllText(_schemaPath, BuildOutputSchema().ToJsonString(_jsonOptions), new UTF8Encoding(false));
                return _schemaPath;
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_process != null && !_process.HasExited && _isInitialized)
            {
                return;
            }

            await _processGate.WaitAsync(cancellationToken);
            try
            {
                if (_process != null && !_process.HasExited && _isInitialized)
                {
                    return;
                }

                var executable = ResolveCodexExecutable();
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = "app-server",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardInputEncoding = Encoding.UTF8,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                    EnableRaisingEvents = true,
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start codex app-server.");
                }

                _process = process;
                _stdin = process.StandardInput;
                _stdoutPump = Task.Run(PumpStdoutAsync);
                _stderrPump = Task.Run(PumpStderrAsync);

                await SendRequestAsync(
                    "initialize",
                    new JsonObject
                    {
                        ["clientInfo"] = new JsonObject
                        {
                            ["name"] = "Revit Agentic AI Companion",
                            ["version"] = ClientVersion,
                        },
                        ["capabilities"] = new JsonObject
                        {
                            ["optOutNotificationMethods"] = new JsonArray(
                                "item/agentMessage/delta",
                                "thread/tokenUsage/updated",
                                "turn/diff/updated"),
                        },
                    },
                    cancellationToken);

                await SendNotificationAsync("initialized", null, cancellationToken);
                _isInitialized = true;
            }
            finally
            {
                _processGate.Release();
            }
        }

        private async Task<string> GetOrCreateThreadIdAsync(string projectKey, CancellationToken cancellationToken)
        {
            var threadId = _threadStore.GetThreadId(projectKey);
            if (!string.IsNullOrWhiteSpace(threadId))
            {
                return threadId;
            }

            threadId = await StartThreadAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(threadId))
            {
                throw new InvalidOperationException("Codex did not return a thread id.");
            }

            _threadStore.SetThreadId(projectKey, threadId);
            return threadId;
        }

        private static JsonObject BuildTurnParams(string threadId, string prompt, JsonNode schema)
        {
            return new JsonObject
            {
                ["threadId"] = threadId,
                ["approvalPolicy"] = "never",
                ["sandboxPolicy"] = new JsonObject
                {
                    ["type"] = "readOnly",
                    ["access"] = new JsonObject
                    {
                        ["type"] = "fullAccess",
                    },
                },
                ["input"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = prompt,
                    },
                },
                ["outputSchema"] = schema?.DeepClone(),
            };
        }

        private async Task<string> StartThreadAsync(CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(string method, JsonNode parameters)
            {
                if (!string.Equals(method, "thread/started", StringComparison.Ordinal))
                {
                    return;
                }

                var notificationThreadId = ExtractThreadId(parameters);
                if (!string.IsNullOrWhiteSpace(notificationThreadId))
                {
                    completion.TrySetResult(notificationThreadId);
                }
            }

            SetNotificationHandler(Handler);
            try
            {
                var response = await SendRequestAsync("thread/start", new JsonObject(), cancellationToken);
                var responseThreadId = ExtractThreadId(response);
                if (!string.IsNullOrWhiteSpace(responseThreadId))
                {
                    return responseThreadId;
                }

                using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
                {
                    return await completion.Task;
                }
            }
            finally
            {
                ClearNotificationHandler(Handler);
            }
        }

        private async Task<string> RunStructuredTurnAsync(JsonObject turnParams, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            string latestStructuredText = null;
            string latestTurnError = null;

            void Handler(string method, JsonNode parameters)
            {
                if (string.Equals(method, "item/completed", StringComparison.Ordinal))
                {
                    var item = GetProperty(parameters, "item");
                    var itemType = GetString(item, "type") ?? GetString(item, "kind");
                    if (string.Equals(itemType, "agent_message", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemType, "agentMessage", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemType, "AgentMessage", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemType, "message", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemType, "Message", StringComparison.OrdinalIgnoreCase))
                    {
                        var text = TryExtractStructuredPayloadText(item);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            latestStructuredText = text.Trim();
                        }
                    }
                }

                if (string.Equals(method, "turn/completed", StringComparison.Ordinal))
                {
                    var turn = GetProperty(parameters, "turn");
                    var turnStatus = GetString(turn, "status");
                    var turnError = ExtractErrorMessage(GetProperty(turn, "error"));
                    if (!string.IsNullOrWhiteSpace(turnError))
                    {
                        latestTurnError = turnError;
                    }

                    if (string.Equals(turnStatus, "failed", StringComparison.OrdinalIgnoreCase) ||
                        !string.IsNullOrWhiteSpace(latestTurnError))
                    {
                        completion.TrySetException(new InvalidOperationException(FirstNonEmpty(latestTurnError, "Codex turn failed.")));
                    }
                    else if (!string.IsNullOrWhiteSpace(latestStructuredText))
                    {
                        completion.TrySetResult(latestStructuredText);
                    }
                    else
                    {
                        completion.TrySetException(new InvalidOperationException("Codex completed the turn without returning a structured agent message."));
                    }
                }

                if (string.Equals(method, "error", StringComparison.Ordinal))
                {
                    latestTurnError = ExtractErrorMessage(GetProperty(parameters, "error"));
                }

                if (string.Equals(method, "turn/failed", StringComparison.Ordinal) ||
                    string.Equals(method, "turn/error", StringComparison.Ordinal))
                {
                    latestTurnError = ExtractErrorMessage(parameters);
                    completion.TrySetException(new InvalidOperationException(FirstNonEmpty(latestTurnError, "Codex turn failed.")));
                }
            }

            SetNotificationHandler(Handler);
            try
            {
                await SendRequestAsync("turn/start", turnParams, cancellationToken);
                using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
                {
                    return await completion.Task;
                }
            }
            finally
            {
                ClearNotificationHandler(Handler);
            }
        }

        private async Task<JsonNode> SendRequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            if (_process == null || _process.HasExited || _stdin == null)
            {
                throw new InvalidOperationException("Codex app-server is not running.");
            }

            var id = Interlocked.Increment(ref _nextRequestId);
            var completion = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[id] = completion;

            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters ?? new JsonObject(),
            };

            await _stdin.WriteLineAsync(payload.ToJsonString());
            await _stdin.FlushAsync();

            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
            {
                var finishedTask = await Task.WhenAny(completion.Task, Task.Delay(RequestTimeout, CancellationToken.None));
                if (finishedTask != completion.Task)
                {
                    _pendingRequests.TryRemove(id, out _);
                    var detail = FirstNonEmpty(
                        _lastTransportError,
                        "No response was received from codex app-server.");
                    ResetConnection("Timed out waiting for codex app-server response to '" + method + "'. " + detail);
                    throw new TimeoutException("Timed out waiting for codex app-server response to '" + method + "'.");
                }

                return await completion.Task;
            }
        }

        private async Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            if (_process == null || _process.HasExited || _stdin == null)
            {
                throw new InvalidOperationException("Codex app-server is not running.");
            }

            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
            };

            if (parameters != null)
            {
                payload["params"] = parameters;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _stdin.WriteLineAsync(payload.ToJsonString());
            await _stdin.FlushAsync();
        }

        private async Task PumpStdoutAsync()
        {
            try
            {
                while (_process != null && !_process.HasExited)
                {
                    var line = await _process.StandardOutput.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    JsonNode message;
                    try
                    {
                        message = JsonNode.Parse(line);
                    }
                    catch (Exception ex)
                    {
                        _lastTransportError = "Failed to parse Codex app-server output: " + ex.Message;
                        continue;
                    }

                    var idNode = message?["id"] as JsonValue;
                    long id;
                    TaskCompletionSource<JsonNode> completion;
                    if (idNode != null && idNode.TryGetValue(out id) && _pendingRequests.TryRemove(id, out completion))
                    {
                        var error = message?["error"];
                        if (error != null)
                        {
                            completion.TrySetException(new InvalidOperationException(error.ToJsonString()));
                            continue;
                        }

                        completion.TrySetResult(message?["result"]);
                        continue;
                    }

                    var method = GetString(message, "method");
                    if (string.IsNullOrWhiteSpace(method))
                    {
                        continue;
                    }

                    Action<string, JsonNode> handler;
                    lock (_notificationGate)
                    {
                        handler = _notificationHandler;
                    }

                    handler?.Invoke(method, message?["params"]);
                }
            }
            catch (Exception ex)
            {
                _lastTransportError = ex.Message;
                FailPendingRequests(ex);
            }
        }

        private async Task PumpStderrAsync()
        {
            try
            {
                while (_process != null && !_process.HasExited)
                {
                    var line = await _process.StandardError.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _lastTransportError = line;
                    }
                }
            }
            catch
            {
            }
        }

        private void FailPendingRequests(Exception ex)
        {
            foreach (var pair in _pendingRequests.ToArray())
            {
                if (_pendingRequests.TryRemove(pair.Key, out var completion))
                {
                    completion.TrySetException(ex);
                }
            }
        }

        private void ResetConnection(string reason)
        {
            _lastTransportError = reason;
            _isInitialized = false;

            lock (_notificationGate)
            {
                _notificationHandler = null;
            }

            try
            {
                _stdin?.Dispose();
            }
            catch
            {
            }

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            catch
            {
            }

            _stdin = null;
            _process = null;
            _stdoutPump = null;
            _stderrPump = null;
        }

        private void SetNotificationHandler(Action<string, JsonNode> handler)
        {
            lock (_notificationGate)
            {
                _notificationHandler = handler;
            }
        }

        private void ClearNotificationHandler(Action<string, JsonNode> handler)
        {
            lock (_notificationGate)
            {
                if (_notificationHandler == handler)
                {
                    _notificationHandler = null;
                }
            }
        }

        private static CodexTurnResult ParseTurnOutput(string output)
        {
            var threadId = string.Empty;
            var structuredPayload = string.Empty;
            var error = string.Empty;
            var lines = (output ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                JsonNode node;
                try
                {
                    node = JsonNode.Parse(rawLine);
                }
                catch
                {
                    continue;
                }

                var type = GetString(node, "type");
                if (string.Equals(type, "thread.started", StringComparison.OrdinalIgnoreCase))
                {
                    threadId = FirstNonEmpty(
                        GetString(node, "thread_id"),
                        GetString(node, "threadId"),
                        threadId);
                    continue;
                }

                if (string.Equals(type, "item.completed", StringComparison.OrdinalIgnoreCase))
                {
                    var item = GetProperty(node, "item");
                    var itemType = FirstNonEmpty(GetString(item, "type"), GetString(item, "kind"));
                    if (string.Equals(itemType, "agent_message", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemType, "message", StringComparison.OrdinalIgnoreCase))
                    {
                        var text = FirstNonEmpty(GetString(item, "text"), ExtractText(item));
                        if (LooksLikeJson(text))
                        {
                            structuredPayload = text.Trim();
                        }
                    }

                    continue;
                }

                if (string.Equals(type, "turn.completed", StringComparison.OrdinalIgnoreCase))
                {
                    error = FirstNonEmpty(error, GetString(node, "error"));
                }
            }

            return new CodexTurnResult(threadId, structuredPayload, error);
        }

        private static bool LooksLikeMissingThread(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                (message.IndexOf("thread not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf("No session found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf("unknown session", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool LooksLikeJson(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                text.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
            }
        }

        private static string ResolveCodexExecutable()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                var sandboxBin = Path.Combine(userProfile, ".codex", ".sandbox-bin", "codex.exe");
                if (File.Exists(sandboxBin))
                {
                    return sandboxBin;
                }

                var sandboxShim = Path.Combine(userProfile, ".codex", ".sandbox-bin", "codex");
                if (File.Exists(sandboxShim))
                {
                    return sandboxShim;
                }
            }

            var commandPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in commandPath.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                var candidate = Path.Combine(directory, "codex.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                candidate = Path.Combine(directory, "codex");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("Could not find codex on PATH.");
        }

        private static string BuildProjectKey(RevitContextSnapshot snapshot)
        {
            var path = snapshot?.DocumentPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path.Trim().ToLowerInvariant();
            }

            return snapshot?.DocumentTitle ?? "no-document";
        }

        private static string BuildPlanningPrompt(PlanningRequest request)
        {
            var snapshot = request.ContextSnapshot;
            var builder = new StringBuilder();
            builder.AppendLine("You are the planning runtime for a Revit add-in with modeless chat.");
            builder.AppendLine("Return JSON only and obey the provided output schema.");
            builder.AppendLine("Accept any user prompt. Do not reject prompts just because they are not Revit write tasks.");
            builder.AppendLine("Choose one of three response kinds:");
            builder.AppendLine("- reply_only: for greetings, general questions, clarification, read-only guidance, or anything that should not execute code.");
            builder.AppendLine("- read_only_query: when the user needs live model inspection or analysis without changing the Revit document.");
            builder.AppendLine("- action_proposal: only when you are confident the user wants a Revit model change and the request fits the current demo capability.");
            builder.AppendLine("Current executable demo capabilities:");
            builder.AppendLine("- create or replace a single schedule/BOQ ViewSchedule in Revit");
            builder.AppendLine("- inspect the active document, current selection, parameters, views, systems, and warnings in read-only mode");
            builder.AppendLine("- batch-update one writable parameter across selected elements or a clearly bounded matching set");
            builder.AppendLine("- clash coordination in a bounded scope such as the current selection, active view, or an explicitly named confined area");
            builder.AppendLine("- higher-risk creation workflows such as localized family/device placement, bounded wall/system creation, and limited MEP adjustments");
            builder.AppendLine("This host compiles and executes C# in-process, but the host owns the transaction lifecycle.");
            builder.AppendLine("Rules:");
            builder.AppendLine("- Always populate every schema field.");
            builder.AppendLine("- Always set capabilityBand, riskLevel, and scopeSummary.");
            builder.AppendLine("- For reply_only, set messageText to the user-facing reply, actionSummary = \"\", transactionName = \"\", generatedSource = \"\", isUndoHostile = false, capabilityBand = \"reply\", riskLevel = \"low\", and scopeSummary = \"\".");
            builder.AppendLine("- For read_only_query, set messageText to a short explanation of what will be inspected, set transactionName = \"\", isUndoHostile = false, set capabilityBand = \"read_query\", set riskLevel = \"low\", set a useful scopeSummary, and generate source for GeneratedActions.CompanionAction.Execute(UIApplication uiapp).");
            builder.AppendLine("- For action_proposal, messageText should briefly explain the planned action before approval.");
            builder.AppendLine("- For action_proposal, set actionSummary, transactionName, generatedSource, isUndoHostile, capabilityBand, riskLevel, and scopeSummary explicitly.");
            builder.AppendLine("- For action_proposal, generate two static entry points: GeneratedActions.CompanionAction.Preview(UIApplication uiapp) and GeneratedActions.CompanionAction.Execute(UIApplication uiapp).");
            builder.AppendLine("- Do not create Transaction or TransactionGroup objects.");
            builder.AppendLine("- The code may use the raw Revit API.");
            builder.AppendLine("- Keep write scope bounded and safe.");
            builder.AppendLine("- For schedule actions, keep scope limited to creating or replacing a single ViewSchedule.");
            builder.AppendLine("- For parameter edits, prefer the current selection first. Otherwise keep the target set to one clearly named category and one parameter.");
            builder.AppendLine("- For parameter edits, Preview must only inspect and report the affected elements and values. Execute performs the write.");
            builder.AppendLine("- For clash coordination, capabilityBand must be \"clash_coordination\". Keep scope to selected elements, active view, or a clearly stated confined area. Preview must report target element ids, the clash/fix summary, and exactly what would change. Execute must perform one bounded fix set only.");
            builder.AppendLine("- For higher-risk creation workflows, capabilityBand must be \"creation_workflow\". Keep scope local to the current selection, current view, current level, current room, or an explicitly requested confined area. Prefer creating or adjusting a small number of elements over broad refactors.");
            builder.AppendLine("- For higher-risk creation workflows, set riskLevel to \"high\" unless the work is clearly localized and reversible. Preview must name anchors, targets, and likely created or modified element ids when possible.");
            builder.AppendLine("- Return human-readable transaction names.");
            builder.AppendLine("- The generated Execute method must return GeneratedActionResult with changed element ids as IReadOnlyList<long>.");
            builder.AppendLine("- The generated Preview method must return GeneratedActionResult with target element ids as IReadOnlyList<long>.");
            builder.AppendLine("- Use elementId.Value, not IntegerValue.");
            builder.AppendLine("- Do not use SchedulableField.GetFieldType().");
            builder.AppendLine("- Do not use ElementId.IntegerValue.");
            builder.AppendLine("- Prefer straightforward, compile-safe Revit API usage over complex heuristics.");
            builder.AppendLine("- If you can answer conversationally without model mutation, prefer reply_only.");
            builder.AppendLine();
            builder.AppendLine("Prompt:");
            builder.AppendLine(request.Prompt ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Context:");
            builder.AppendLine("DocumentTitle: " + snapshot.DocumentTitle);
            builder.AppendLine("DocumentPath: " + snapshot.DocumentPath);
            builder.AppendLine("ActiveView: " + snapshot.ActiveViewName);
            builder.AppendLine("SelectedCategories: " + string.Join(", ", snapshot.SelectedCategoryNames));
            builder.AppendLine("AvailableCategories: " + string.Join(", ", snapshot.AvailableModelCategories.Take(150)));
            builder.AppendLine();
            builder.AppendLine("The generated source must include:");
            builder.AppendLine("using System;");
            builder.AppendLine("using Autodesk.Revit.DB;");
            builder.AppendLine("using Autodesk.Revit.UI;");
            builder.AppendLine("using RevitAgenticAICompanion.Runtime;");
            builder.AppendLine();
            builder.AppendLine("Compile-safe guide:");
            builder.AppendLine("- The method must return new GeneratedActionResult(summary, changedElementIdsAsLongs).");
            builder.AppendLine("- Example: new GeneratedActionResult(summary, new long[] { schedule.Id.Value });");
            builder.AppendLine("- If an existing schedule with the same name exists, delete it before creating the replacement schedule.");
            builder.AppendLine("- Compare ElementId values using .Value.");
            builder.AppendLine("- If adding schedule fields, use schedulableField.ParameterId and simple field checks.");
            builder.AppendLine("- For parameter edits, use LookupParameter and StorageType checks before Set.");
            builder.AppendLine("- Keep Unicode string literals intact.");
            return builder.ToString();
        }

        private static string BuildRepairPrompt(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            GeneratedActionCompilationResult compilation)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Repair the previous Revit generated action so it compiles in the current host.");
            builder.AppendLine("Return JSON only and obey the provided output schema.");
            builder.AppendLine("Keep the same intent unless a compiler error proves the code used an invalid API.");
            builder.AppendLine("Requirements:");
            builder.AppendLine("- Keep the same responseKind unless compiler diagnostics prove the previous kind was wrong.");
            builder.AppendLine("- Return a concise messageText that explains the repaired plan.");
            builder.AppendLine("- Populate every schema field, including actionSummary, transactionName, generatedSource, isUndoHostile, capabilityBand, riskLevel, and scopeSummary.");
            builder.AppendLine("- Keep the same entry points: GeneratedActions.CompanionAction.Execute(UIApplication uiapp), and if this is an action_proposal also GeneratedActions.CompanionAction.Preview(UIApplication uiapp).");
            builder.AppendLine("- Do not create Transaction or TransactionGroup objects.");
            builder.AppendLine("- Return GeneratedActionResult with IReadOnlyList<long> changed element ids.");
            builder.AppendLine("- Use elementId.Value, not IntegerValue.");
            builder.AppendLine("- Do not call SchedulableField.GetFieldType().");
            builder.AppendLine("- Keep Unicode characters intact in user-facing strings.");
            builder.AppendLine("- Prefer simple compile-safe schedule logic over ambitious field inference.");
            builder.AppendLine();
            builder.AppendLine("Original user prompt:");
            builder.AppendLine(request.Prompt ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Previous action summary:");
            builder.AppendLine(failedProposal.ActionSummary ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Previous generated source:");
            builder.AppendLine(failedProposal.GeneratedSource ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Compiler diagnostics:");
            foreach (var diagnostic in compilation.Diagnostics ?? Array.Empty<string>())
            {
                builder.AppendLine("- " + diagnostic);
            }

            return builder.ToString();
        }

        private static JsonObject BuildOutputSchema()
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("responseKind", "messageText", "actionSummary", "transactionName", "generatedSource", "isUndoHostile", "capabilityBand", "riskLevel", "scopeSummary"),
                ["properties"] = new JsonObject
                {
                    ["responseKind"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("reply_only", "read_only_query", "action_proposal"),
                    },
                    ["messageText"] = new JsonObject
                    {
                        ["type"] = "string",
                    },
                    ["actionSummary"] = new JsonObject
                    {
                        ["type"] = new JsonArray("string", "null"),
                    },
                    ["transactionName"] = new JsonObject
                    {
                        ["type"] = new JsonArray("string", "null"),
                    },
                    ["generatedSource"] = new JsonObject
                    {
                        ["type"] = new JsonArray("string", "null"),
                    },
                    ["isUndoHostile"] = new JsonObject
                    {
                        ["type"] = "boolean",
                    },
                    ["capabilityBand"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("reply", "read_query", "schedule_workflow", "parameter_edit", "clash_coordination", "creation_workflow"),
                    },
                    ["riskLevel"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("low", "medium", "high"),
                    },
                    ["scopeSummary"] = new JsonObject
                    {
                        ["type"] = "string",
                    },
                },
            };
        }

        private static ProposalCandidate BuildProposalCandidate(string userPrompt, CodexPlanningPayload payload, int repairCount)
        {
            if (IsReplyOnly(payload))
            {
                return ProposalCandidate.CreateReply(
                    userPrompt,
                    payload?.MessageText ?? string.Empty,
                    payload?.CapabilityBand ?? "reply",
                    payload?.RiskLevel ?? "low",
                    payload?.ScopeSummary ?? string.Empty,
                    new ProposalProvenance("Codex", repairCount));
            }

            if (string.IsNullOrWhiteSpace(payload?.GeneratedSource))
            {
                throw new InvalidOperationException("Codex returned generated-code response without source.");
            }

            if (IsReadOnlyQuery(payload))
            {
                return ProposalCandidate.CreateReadOnlyQuery(
                    userPrompt,
                    FirstNonEmpty(payload.ActionSummary, payload.MessageText),
                    payload.GeneratedSource,
                    "GeneratedActions.CompanionAction",
                    "Execute",
                    payload.CapabilityBand,
                    payload.RiskLevel,
                    payload.ScopeSummary,
                    new ProposalProvenance("Codex", repairCount));
            }

            return ProposalCandidate.CreateAction(
                userPrompt,
                FirstNonEmpty(payload.ActionSummary, payload.MessageText),
                payload.GeneratedSource,
                new[] { payload.TransactionName ?? string.Empty }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                payload.IsUndoHostile,
                "GeneratedActions.CompanionAction",
                "Execute",
                "Preview",
                payload.CapabilityBand,
                payload.RiskLevel,
                payload.ScopeSummary,
                new ProposalProvenance("Codex", repairCount));
        }

        private static bool IsReplyOnly(CodexPlanningPayload payload)
        {
            return payload == null ||
                string.Equals(payload.ResponseKind, "reply_only", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReadOnlyQuery(CodexPlanningPayload payload)
        {
            return payload != null &&
                string.Equals(payload.ResponseKind, "read_only_query", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActionProposal(CodexPlanningPayload payload)
        {
            return payload != null &&
                string.Equals(payload.ResponseKind, "action_proposal", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresGeneratedCode(CodexPlanningPayload payload)
        {
            return IsReadOnlyQuery(payload) || IsActionProposal(payload);
        }

        private static JsonNode GetProperty(JsonNode node, string propertyName)
        {
            return node is JsonObject jsonObject && jsonObject.TryGetPropertyValue(propertyName, out var propertyValue)
                ? propertyValue
                : null;
        }

        private static string GetString(JsonNode node, string propertyName)
        {
            var property = GetProperty(node, propertyName);
            return property == null ? string.Empty : property.GetValue<string>();
        }

        private static string ExtractThreadId(JsonNode node)
        {
            return FirstNonEmpty(
                GetString(node, "threadId"),
                GetString(node, "thread_id"),
                GetString(GetProperty(node, "thread"), "id"),
                GetString(GetProperty(node, "thread"), "threadId"),
                GetString(GetProperty(node, "thread"), "thread_id"));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool IsThreadNotFoundError(InvalidOperationException ex)
        {
            return ex != null &&
                ex.Message.IndexOf("thread not found", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractText(JsonNode item)
        {
            var directText = GetString(item, "text");
            if (!string.IsNullOrWhiteSpace(directText))
            {
                return directText;
            }

            var content = GetProperty(item, "content") as JsonArray;
            if (content == null)
            {
                return string.Empty;
            }

            foreach (var entry in content)
            {
                var text = GetString(entry, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string TryExtractStructuredPayloadText(JsonNode item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item is JsonObject objectItem &&
                (GetProperty(objectItem, "responseKind") != null ||
                 GetProperty(objectItem, "messageText") != null ||
                 GetProperty(objectItem, "generatedSource") != null))
            {
                return objectItem.ToJsonString();
            }

            var text = ExtractText(item);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            try
            {
                var parsed = JsonNode.Parse(text);
                return parsed == null ? string.Empty : text;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ExtractErrorMessage(JsonNode errorNode)
        {
            if (errorNode == null)
            {
                return string.Empty;
            }

            var directMessage = GetString(errorNode, "message");
            if (!string.IsNullOrWhiteSpace(directMessage))
            {
                return directMessage;
            }

            var nestedError = GetProperty(errorNode, "error");
            return nestedError == null ? string.Empty : GetString(nestedError, "message");
        }

        private sealed class CodexPlanningPayload
        {
            public string ResponseKind { get; set; }
            public string MessageText { get; set; }
            public string ActionSummary { get; set; }
            public string TransactionName { get; set; }
            public string GeneratedSource { get; set; }
            public bool IsUndoHostile { get; set; }
            public string CapabilityBand { get; set; }
            public string RiskLevel { get; set; }
            public string ScopeSummary { get; set; }
        }

        private sealed class CodexCliResult
        {
            public CodexCliResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
                StandardError = standardError ?? string.Empty;
            }

            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
        }

        private sealed class CodexTurnResult
        {
            public CodexTurnResult(string threadId, string structuredPayload, string error)
            {
                ThreadId = threadId ?? string.Empty;
                StructuredPayload = structuredPayload ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public string ThreadId { get; }
            public string StructuredPayload { get; }
            public string Error { get; }
        }
    }
}
