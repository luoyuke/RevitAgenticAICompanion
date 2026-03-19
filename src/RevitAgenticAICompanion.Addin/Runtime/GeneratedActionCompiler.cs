using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class GeneratedActionCompiler
    {
        public GeneratedActionCompilationResult Compile(ProposalCandidate proposal)
        {
            if (proposal == null)
            {
                return new GeneratedActionCompilationResult(false, new[] { "No proposal was provided for compilation." }, null);
            }

            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var syntaxTree = CSharpSyntaxTree.ParseText(proposal.GeneratedSource ?? string.Empty, parseOptions);
            var compilation = CSharpCompilation.Create(
                "GeneratedAction_" + proposal.ProposalId,
                new[] { syntaxTree },
                GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

            using (var assemblyStream = new MemoryStream())
            {
                var emitResult = compilation.Emit(assemblyStream);
                var diagnostics = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                    .Select(diagnostic => diagnostic.ToString())
                    .ToArray();

                if (!emitResult.Success)
                {
                    return new GeneratedActionCompilationResult(false, diagnostics, null);
                }

                return new GeneratedActionCompilationResult(true, diagnostics, assemblyStream.ToArray());
            }
        }

        private static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
                .Split(Path.PathSeparator)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var path in trustedAssemblies)
            {
                yield return MetadataReference.CreateFromFile(path);
            }

            var hostAssembly = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrWhiteSpace(hostAssembly))
            {
                yield return MetadataReference.CreateFromFile(hostAssembly);
            }

            yield return MetadataReference.CreateFromFile(typeof(Element).Assembly.Location);
            yield return MetadataReference.CreateFromFile(typeof(UIApplication).Assembly.Location);
        }
    }
}
