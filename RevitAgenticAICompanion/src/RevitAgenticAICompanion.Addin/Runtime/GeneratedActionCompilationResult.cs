using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class GeneratedActionCompilationResult
    {
        public GeneratedActionCompilationResult(bool isSuccess, IReadOnlyList<string> diagnostics, byte[] assemblyBytes)
        {
            IsSuccess = isSuccess;
            Diagnostics = diagnostics ?? new string[0];
            AssemblyBytes = assemblyBytes;
        }

        public bool IsSuccess { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public byte[] AssemblyBytes { get; }

        public static GeneratedActionCompilationResult NotApplicable()
        {
            return new GeneratedActionCompilationResult(true, new[] { "Compilation not required for reply-only responses." }, null);
        }
    }
}
