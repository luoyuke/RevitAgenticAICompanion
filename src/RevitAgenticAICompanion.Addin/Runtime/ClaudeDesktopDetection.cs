using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ClaudeDesktopDetection
    {
        public ClaudeDesktopDetection(bool isDetected, IReadOnlyList<string> findings)
        {
            IsDetected = isDetected;
            Findings = findings ?? new string[0];
        }

        public bool IsDetected { get; }
        public IReadOnlyList<string> Findings { get; }
    }

    [SupportedOSPlatform("windows")]
    public static class ClaudeDesktopDetector
    {
        public static ClaudeDesktopDetection Detect()
        {
            var findings = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddKnownPathFindings(findings, seen);
            AddStartMenuFindings(findings, seen);
            AddRegistryFindings(findings, seen);

            return new ClaudeDesktopDetection(findings.Count > 0, findings);
        }

        private static void AddKnownPathFindings(List<string> findings, HashSet<string> seen)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                AddExistingPath(findings, seen, Path.Combine(localAppData, "Programs", "Claude", "Claude.exe"), "Claude Desktop executable");
                AddExistingPath(findings, seen, Path.Combine(localAppData, "Claude", "Claude.exe"), "Claude Desktop executable");

                try
                {
                    foreach (var directory in Directory.EnumerateDirectories(localAppData, "Claude*", SearchOption.TopDirectoryOnly))
                    {
                        AddExistingPath(findings, seen, directory, "Claude Desktop directory");
                    }
                }
                catch
                {
                }
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                AddExistingPath(findings, seen, Path.Combine(programFiles, "Claude", "Claude.exe"), "Claude Desktop executable");
                AddExistingPath(findings, seen, Path.Combine(programFiles, "Anthropic", "Claude", "Claude.exe"), "Claude Desktop executable");
            }
        }

        private static void AddStartMenuFindings(List<string> findings, HashSet<string> seen)
        {
            AddStartMenuLinks(findings, seen, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            AddStartMenuLinks(findings, seen, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
        }

        private static void AddStartMenuLinks(List<string> findings, HashSet<string> seen, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return;
            }

            try
            {
                foreach (var link in Directory.EnumerateFiles(root, "*Claude*.lnk", SearchOption.AllDirectories))
                {
                    AddFinding(findings, seen, "Claude Start Menu shortcut: " + link);
                }
            }
            catch
            {
            }
        }

        private static void AddRegistryFindings(List<string> findings, HashSet<string> seen)
        {
            AddUninstallRegistryFindings(findings, seen, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            AddUninstallRegistryFindings(findings, seen, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            AddUninstallRegistryFindings(findings, seen, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        }

        private static void AddUninstallRegistryFindings(List<string> findings, HashSet<string> seen, RegistryKey root, string subKeyPath)
        {
            try
            {
                using (var uninstall = root.OpenSubKey(subKeyPath))
                {
                    if (uninstall == null)
                    {
                        return;
                    }

                    foreach (var subKeyName in uninstall.GetSubKeyNames())
                    {
                        using (var app = uninstall.OpenSubKey(subKeyName))
                        {
                            var displayName = app?.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(displayName) ||
                                (displayName.IndexOf("Claude", StringComparison.OrdinalIgnoreCase) < 0 &&
                                 displayName.IndexOf("Anthropic", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                continue;
                            }

                            var installLocation = app.GetValue("InstallLocation") as string;
                            AddFinding(findings, seen, "Claude registry entry: " + displayName +
                                (string.IsNullOrWhiteSpace(installLocation) ? string.Empty : " at " + installLocation));
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddExistingPath(List<string> findings, HashSet<string> seen, string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (File.Exists(path) || Directory.Exists(path))
            {
                AddFinding(findings, seen, label + ": " + path);
            }
        }

        private static void AddFinding(List<string> findings, HashSet<string> seen, string finding)
        {
            if (!string.IsNullOrWhiteSpace(finding) && seen.Add(finding))
            {
                findings.Add(finding);
            }
        }
    }
}
