using System;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class CodexRuntimeException : InvalidOperationException
    {
        public CodexRuntimeException(string message, CodexRuntimeFailureRecord failureRecord)
            : base(message)
        {
            FailureRecord = failureRecord;
        }

        public CodexRuntimeFailureRecord FailureRecord { get; }
    }
}
