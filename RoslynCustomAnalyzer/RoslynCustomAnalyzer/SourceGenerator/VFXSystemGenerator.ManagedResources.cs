using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynCustomAnalyzer.SourceGenerator
{
    public partial class VfxSystemGenerator
    {
        private void GenerateManagedResources(GeneratorExecutionContext context, List<(StructDeclarationSyntax, AttributeData)> identifiedStructs)
        {
            var fields = new StringBuilder();
            var assignments = new StringBuilder();

            foreach (var (structDeclaration, attribute) in identifiedStructs)
            {
                var semanticModel = context.Compilation.GetSemanticModel(structDeclaration.SyntaxTree);
                var symbol = semanticModel.GetDeclaredSymbol(structDeclaration) as ITypeSymbol;
                var isParented = symbol?.AllInterfaces.Any(i => i.Name == "IKillableVFX");
                var systemType = isParented == true ? "Parented" : "Parentless";

                var menuName = attribute.ConstructorArguments[0].Value?.ToString() ?? "ERROR";
                var singletonName = $"VFX{menuName}Singleton";
                var graphFieldName = $"{menuName}Graph";

                fields.AppendLine($"        public VisualEffect {graphFieldName};");

                if (systemType == "Parentless")
                {
                    assignments.AppendLine($"            VfxParentlessReferences<{singletonName}>.VfxGraph = {graphFieldName};");
                }
                else
                {
                    assignments.AppendLine($"            VfxParentedReferences<{singletonName}>.VfxGraph = {graphFieldName};");
                }
            }

            var source = $@"using UnityEngine.VFX;
using Core.Scripts.Systems.External.Vfx.Base.Utils;

namespace Core.Scripts.Systems.External.Vfx.Base.Utils
{{
    public partial class ManagedResources
    {{
{fields}
        public void AwakeGenerated()
        {{
{assignments}
        }}
    }}
}}";


            context.AddSource("ManagedResources.g.cs", source);
        }
    }
}