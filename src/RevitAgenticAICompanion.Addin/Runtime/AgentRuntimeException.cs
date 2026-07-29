using System;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class AgentRuntimeException : InvalidOperationException
    {
        public AgentRuntimeException(string message, AgentRuntimeFailureRecord failureRecord = null)
            : base(message)
        {
            FailureRecord = failureRecord;
        }

        public AgentRuntimeFailureRecord FailureRecord { get; }
    }
}
