using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAgenticAICompanion.Runtime
{
    [SupportedOSPlatform("windows")]
    public sealed class ClaudeExecutableResolver
    {
        public const string OverrideEnvironmentVariable = "REVIT_AGENTIC_AI_CLAUDE_PATH";
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

        public async Task<ClaudeExecutableResolution> ResolveAsync(CancellationToken cancellationToken)
        {
            var diagnostics = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var desktopDetection = ClaudeDesktopDetector.Detect();
            if (desktopDetection.IsDetected)
            {
                diagnostics.Add("Claude Desktop/Windows app detected. The Revit runtime still requires Claude Code CLI because the desktop app is not a scriptable JSON/stdout runtime.");
                diagnostics.AddRange(desktopDetection.Findings);
            }
            else
            {
                diagnostics.Add("Claude Desktop/Windows app not detected.");
            }

            var overridePath = NormalizePath(Environment.GetEnvironmentVariable(OverrideEnvironmentVariable));
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                var overrideProbe = await ProbeAsync(overridePath, cancellationToken);
                if (!overrideProbe.Success)
                {
                    throw new FileNotFoundException("The explicit Claude CLI override (" + OverrideEnvironmentVariable + ") is not usable: " + overridePath + " (" + overrideProbe.Error + ")");
                }

                diagnostics.Add("Selected explicit override: " + overridePath);
                return new ClaudeExecutableResolution(overridePath, overrideProbe.Version, "explicit override", diagnostics);
            }

            foreach (var candidate in EnumerateCandidates(seen))
            {
                var probe = await ProbeAsync(candidate.Path, cancellationToken);
                if (probe.Success)
                {
                    diagnostics.Add("Selected " + candidate.Source + ": " + candidate.Path);
                    return new ClaudeExecutableResolution(candidate.Path, probe.Version, candidate.Source, diagnostics);
                }

                diagnostics.Add("Skipped " + candidate.Source + ": " + candidate.Path + " (" + probe.Error + ")");
            }

            var message = "Could not find a working Claude Code CLI. Install Claude Code or set " + OverrideEnvironmentVariable + " to claude.exe/claude.cmd.";
            if (desktopDetection.IsDetected)
            {
                message += " Claude Desktop/Windows app was detected, but it cannot be used directly by this add-in because it does not expose the required non-interactive JSON/stdout interface.";
            }

            throw new FileNotFoundException(message + " Diagnostics: " + string.Join(" | ", diagnostics));
        }

        private static IEnumerable<Candidate> EnumerateCandidates(HashSet<string> seen)
        {
            foreach (var directory in EnumerateInstallDirectories())
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) continue;
                foreach (var name in EnumerateExecutableNames())
                {
                    var path = SafeCombine(directory, name);
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && seen.Add(path)) yield return new Candidate(path, "install directory");
                }
            }

            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                foreach (var name in EnumerateExecutableNames())
                {
                    var path = SafeCombine(directory.Trim(), name);
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && seen.Add(path)) yield return new Candidate(path, "PATH");
                }
            }
        }

        private static IEnumerable<string> EnumerateInstallDirectories()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData)) yield return Path.Combine(appData, "npm");
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData)) yield return Path.Combine(localAppData, "Programs", "claude");
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile)) yield return Path.Combine(userProfile, ".local", "bin");
        }

        private static IEnumerable<string> EnumerateExecutableNames()
        {
            yield return "claude.exe";
            yield return "claude.cmd";
            yield return "claude";
        }

        private static async Task<ProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path)) return ProbeResult.Fail("path is empty");
            if (!File.Exists(path)) return ProbeResult.Fail("file does not exist");
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
                    if (!process.Start()) return ProbeResult.Fail("process did not start");
                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    var waitTask = process.WaitForExitAsync(cancellationToken);
                    if (await Task.WhenAny(waitTask, Task.Delay(ProbeTimeout, CancellationToken.None)) != waitTask)
                    {
                        TryKill(process);
                        return ProbeResult.Fail("version check timed out");
                    }
                    await waitTask;
                    var stdout = (await stdoutTask ?? string.Empty).Trim();
                    var stderr = (await stderrTask ?? string.Empty).Trim();
                    return process.ExitCode == 0 ? ProbeResult.Ok(FirstNonEmpty(stdout, stderr, "unknown")) : ProbeResult.Fail("version check exited " + process.ExitCode + ": " + FirstNonEmpty(stderr, stdout));
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return ProbeResult.Fail(ex.Message);
            }
        }

        private static string SafeCombine(string directory, string name) { try { return Path.Combine(directory, name); } catch { return string.Empty; } }
        private static string NormalizePath(string path)
        {
            var normalized = (path ?? string.Empty).Trim();
            return normalized.Length >= 2 && normalized.StartsWith(""", StringComparison.Ordinal) && normalized.EndsWith(""", StringComparison.Ordinal)
                ? normalized.Substring(1, normalized.Length - 2)
                : normalized;
        }
        private static string FirstNonEmpty(params string[] values) { return values == null ? string.Empty : values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty; }
        private static void TryKill(Process process) { try { if (process != null && !process.HasExited) process.Kill(true); } catch { } }
        private sealed class Candidate { public Candidate(string path, string source) { Path = path; Source = source; } public string Path { get; } public string Source { get; } }
        private sealed class ProbeResult { private ProbeResult(bool success, string version, string error) { Success = success; Version = version ?? string.Empty; Error = error ?? string.Empty; } public bool Success { get; } public string Version { get; } public string Error { get; } public static ProbeResult Ok(string version) { return new ProbeResult(true, version, string.Empty); } public static ProbeResult Fail(string error) { return new ProbeResult(false, string.Empty, error); } }
    }
}
