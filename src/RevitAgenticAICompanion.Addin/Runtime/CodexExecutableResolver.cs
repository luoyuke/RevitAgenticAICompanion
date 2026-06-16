using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class CodexExecutableResolver
    {
        public const string OverrideEnvironmentVariable = "REVIT_AGENTIC_AI_CODEX_PATH";
        private static readonly TimeSpan CandidateProbeTimeout = TimeSpan.FromSeconds(8);

        public async Task<CodexExecutableResolution> ResolveAsync(CancellationToken cancellationToken)
        {
            var diagnostics = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var overridePath = NormalizePath(Environment.GetEnvironmentVariable(OverrideEnvironmentVariable));
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return await ResolveExplicitOverrideAsync(overridePath, diagnostics, cancellationToken);
            }

            foreach (var candidate in EnumerateCandidates(seen))
            {
                var probe = await ProbeCandidateAsync(candidate.Path, cancellationToken);
                if (probe.IsSuccess)
                {
                    diagnostics.Add("Selected " + candidate.Source + ": " + candidate.Path);
                    return new CodexExecutableResolution(
                        candidate.Path,
                        probe.Version,
                        candidate.Source,
                        diagnostics);
                }

                diagnostics.Add("Skipped " + candidate.Source + ": " + candidate.Path + " (" + probe.Error + ")");
            }

            throw new FileNotFoundException(
                "Could not find a working Codex executable. Install the Codex app or set " +
                OverrideEnvironmentVariable + " to a working codex.exe path.");
        }

        private static async Task<CodexExecutableResolution> ResolveExplicitOverrideAsync(
            string overridePath,
            List<string> diagnostics,
            CancellationToken cancellationToken)
        {
            var probe = await ProbeCandidateAsync(overridePath, cancellationToken);
            if (!probe.IsSuccess)
            {
                throw new FileNotFoundException(
                    "The explicit Codex executable override from " + OverrideEnvironmentVariable +
                    " is not usable: " + overridePath + " (" + probe.Error + ")");
            }

            diagnostics.Add("Selected explicit override " + OverrideEnvironmentVariable + ": " + overridePath);
            return new CodexExecutableResolution(
                overridePath,
                probe.Version,
                "explicit override",
                diagnostics);
        }

        private static IEnumerable<CodexExecutableCandidate> EnumerateCandidates(HashSet<string> seen)
        {
            foreach (var candidate in EnumerateCodexAppRuntimeCandidates())
            {
                if (seen.Add(candidate.Path))
                {
                    yield return candidate;
                }
            }

            foreach (var candidate in EnumeratePathCandidates())
            {
                if (seen.Add(candidate.Path))
                {
                    yield return candidate;
                }
            }

            foreach (var candidate in EnumerateLegacySandboxCandidates())
            {
                if (seen.Add(candidate.Path))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<CodexExecutableCandidate> EnumerateCodexAppRuntimeCandidates()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                yield break;
            }

            var binRoot = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
            if (!Directory.Exists(binRoot))
            {
                yield break;
            }

            foreach (var candidate in Directory
                .EnumerateDirectories(binRoot)
                .Select(directory => Path.Combine(directory, "codex.exe"))
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc))
            {
                yield return new CodexExecutableCandidate(candidate.FullName, "Codex app runtime");
            }
        }

        private static IEnumerable<CodexExecutableCandidate> EnumeratePathCandidates()
        {
            var commandPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in commandPath.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                foreach (var fileName in new[] { "codex.exe", "codex" })
                {
                    string candidate;
                    try
                    {
                        candidate = Path.Combine(directory.Trim(), fileName);
                    }
                    catch
                    {
                        continue;
                    }

                    if (File.Exists(candidate))
                    {
                        yield return new CodexExecutableCandidate(candidate, "PATH");
                    }
                }
            }
        }

        private static IEnumerable<CodexExecutableCandidate> EnumerateLegacySandboxCandidates()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userProfile))
            {
                yield break;
            }

            foreach (var fileName in new[] { "codex.exe", "codex" })
            {
                var candidate = Path.Combine(userProfile, ".codex", ".sandbox-bin", fileName);
                if (File.Exists(candidate))
                {
                    yield return new CodexExecutableCandidate(candidate, "legacy sandbox");
                }
            }
        }

        private static async Task<CodexExecutableProbeResult> ProbeCandidateAsync(
            string path,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return CodexExecutableProbeResult.Fail("path is empty");
            }

            if (!File.Exists(path))
            {
                return CodexExecutableProbeResult.Fail("file does not exist");
            }

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    if (!process.Start())
                    {
                        return CodexExecutableProbeResult.Fail("process did not start");
                    }

                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    var waitTask = process.WaitForExitAsync(cancellationToken);
                    var completed = await Task.WhenAny(waitTask, Task.Delay(CandidateProbeTimeout, CancellationToken.None));
                    if (completed != waitTask)
                    {
                        TryKill(process);
                        return CodexExecutableProbeResult.Fail("version check timed out");
                    }

                    await waitTask;
                    var stdout = await stdoutTask;
                    var stderr = await stderrTask;
                    if (process.ExitCode != 0)
                    {
                        return CodexExecutableProbeResult.Fail(
                            "version check exited " + process.ExitCode + ": " + Summarize(FirstNonEmpty(stderr, stdout)));
                    }

                    var version = FirstNonEmpty(Summarize(stdout), Summarize(stderr), "unknown");
                    return CodexExecutableProbeResult.Success(version);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return CodexExecutableProbeResult.Fail(ex.Message);
            }
        }

        private static string NormalizePath(string path)
        {
            var normalized = (path ?? string.Empty).Trim();
            if (normalized.Length >= 2 &&
                normalized.StartsWith("\"", StringComparison.Ordinal) &&
                normalized.EndsWith("\"", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1, normalized.Length - 2);
            }

            return normalized;
        }

        private static string Summarize(string text)
        {
            var normalized = (text ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            const int maxLength = 180;
            return normalized.Length <= maxLength
                ? normalized
                : normalized.Substring(0, maxLength) + "...";
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

        private sealed class CodexExecutableCandidate
        {
            public CodexExecutableCandidate(string path, string source)
            {
                Path = path ?? string.Empty;
                Source = source ?? string.Empty;
            }

            public string Path { get; }
            public string Source { get; }
        }

        private sealed class CodexExecutableProbeResult
        {
            private CodexExecutableProbeResult(bool isSuccess, string version, string error)
            {
                IsSuccess = isSuccess;
                Version = version ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public bool IsSuccess { get; }
            public string Version { get; }
            public string Error { get; }

            public static CodexExecutableProbeResult Success(string version)
            {
                return new CodexExecutableProbeResult(true, version, string.Empty);
            }

            public static CodexExecutableProbeResult Fail(string error)
            {
                return new CodexExecutableProbeResult(false, string.Empty, error);
            }
        }
    }
}
