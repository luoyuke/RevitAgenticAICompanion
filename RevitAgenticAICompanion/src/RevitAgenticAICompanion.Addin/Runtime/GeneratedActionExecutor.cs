using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.Revit.UI;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class GeneratedActionExecutor
    {
        public GeneratedActionResult Execute(GeneratedActionCompilationResult compilation, ProposalCandidate proposal, UIApplication uiApplication)
        {
            return InvokeGeneratedAction(compilation, proposal, uiApplication, proposal?.EntryPointMethodName);
        }

        public GeneratedActionPreviewResult Preview(GeneratedActionCompilationResult compilation, ProposalCandidate proposal, UIApplication uiApplication)
        {
            if (proposal == null)
            {
                throw new InvalidOperationException("No proposal is available for preview.");
            }

            if (string.IsNullOrWhiteSpace(proposal.PreviewMethodName))
            {
                throw new InvalidOperationException("The generated proposal does not define a preview entry point.");
            }

            var preview = InvokeGeneratedAction(compilation, proposal, uiApplication, proposal.PreviewMethodName);
            return new GeneratedActionPreviewResult(true, preview.Summary, preview.ChangedElementIds, string.Empty);
        }

        private static GeneratedActionResult InvokeGeneratedAction(
            GeneratedActionCompilationResult compilation,
            ProposalCandidate proposal,
            UIApplication uiApplication,
            string methodName)
        {
            if (compilation == null || !compilation.IsSuccess || compilation.AssemblyBytes == null)
            {
                throw new InvalidOperationException("No compiled assembly is available for execution.");
            }

            if (proposal == null)
            {
                throw new InvalidOperationException("No proposal is available for execution.");
            }

            using (var stream = new MemoryStream(compilation.AssemblyBytes))
            {
                var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
                var type = assembly.GetType(proposal.EntryPointTypeName, throwOnError: true);
                var method = type.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(UIApplication) },
                    modifiers: null);

                if (method == null)
                {
                    throw new InvalidOperationException("The generated entry point was not found.");
                }

                var result = method.Invoke(null, new object[] { uiApplication });
                if (result is GeneratedActionResult actionResult)
                {
                    return actionResult;
                }

                throw new InvalidOperationException("The generated entry point returned an unexpected result.");
            }
        }
    }
}
