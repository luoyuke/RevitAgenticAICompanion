using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace RevitAgenticAICompanion.Runtime
{
    public sealed class CliProcessResult
    {
        public CliProcessResult(int exitCode, string standardOutput, string standardError, string executablePath, string arguments)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            Arguments = arguments ?? string.Empty;
        }
        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public string ExecutablePath { get; }
        public string Arguments { get; }
        public bool IsSuccess { get { return ExitCode == 0; } }
    }
    public static class CliProcessRunner
    {
        public static async Task<CliProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            string standardInput,
            TimeSpan timeout,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }
            if (arguments != null)
            {
                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument ?? string.Empty);
                }
            }
            var argumentSummary = BuildArgumentSummary(arguments);
            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                {
                    throw new AgentRuntimeException("Failed to start process: " + executablePath);
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
                        // The CLI may have exited before reading stdin; return captured output below.
                    }
                }
                try { process.StandardInput.Close(); } catch { }
                var waitTask = process.WaitForExitAsync(cancellationToken);
                var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, CancellationToken.None));
                if (completed != waitTask)
                {
                    TryKill(process);
                    throw new AgentRuntimeException("Timed out waiting for CLI process: " + executablePath);
                }
                await waitTask;
                return new CliProcessResult(process.ExitCode, await stdoutTask, await stderrTask, executablePath, argumentSummary);
            }
        }
        public static string BuildArgumentSummary(IReadOnlyList<string> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return string.Empty;
            }
            var builder = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                var value = argument ?? string.Empty;
                if (value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0)
                {
                    builder.Append('"');
                    builder.Append(value.Replace("\"", "\\\""));
                    builder.Append('"');
                }
                else
                {
                    builder.Append(value);
                }
            }
            return builder.ToString();
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
    }
}