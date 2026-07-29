using System;
using System.Collections.Generic;
using RevitAgenticAICompanion.Storage;

namespace RevitAgenticAICompanion.Runtime
{
    public static class RuntimeClientFactory
    {
        public static RuntimeClientRouter Create(LocalStoragePaths paths, ProjectThreadStore threadStore, AgentRuntimeProvider selectedProvider)
        {
            var codexPrimary = new CodexAgentRuntimeClient(paths, threadStore);
#pragma warning disable CA1416 // The Revit add-in is Windows-only; suppress the local analyzer hop for the Claude CLI resolver.
            var claudePrimary = new ClaudeAgentRuntimeClient(paths, threadStore);
#pragma warning restore CA1416
            var clients = new Dictionary<AgentRuntimeProvider, IAgentRuntimeClient>
            {
                { AgentRuntimeProvider.Codex, new FallbackAgentRuntimeClient(codexPrimary, new LocalReviewAgentRuntimeClient()) },
                { AgentRuntimeProvider.Claude, new FallbackAgentRuntimeClient(claudePrimary, new LocalReviewAgentRuntimeClient()) },
            };
            return new RuntimeClientRouter(clients, new IDisposable[] { codexPrimary }, selectedProvider);
        }
    }
}
