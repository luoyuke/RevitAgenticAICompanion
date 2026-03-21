using System;
using System.IO;

namespace RevitAgenticAICompanion.Storage
{
    public sealed class LocalStoragePaths
    {
        public LocalStoragePaths(string appFolderName)
        {
            RootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appFolderName);

            ArtifactsPath = Path.Combine(RootPath, "artifacts");
            StatePath = Path.Combine(RootPath, "state");
            AuditDatabasePath = Path.Combine(StatePath, "audit.db");
            UserMemoryPath = Path.Combine(StatePath, "memory.md");
        }

        public string RootPath { get; }
        public string ArtifactsPath { get; }
        public string StatePath { get; }
        public string AuditDatabasePath { get; }
        public string UserMemoryPath { get; }

        public void EnsureCreated()
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(ArtifactsPath);
            Directory.CreateDirectory(StatePath);
        }
    }
}
