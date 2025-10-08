using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynCustomAnalyzer.SourceGenerator
{
    public partial class VfxSystemGenerator
    {
        private void GenerateSingleton(GeneratorExecutionContext context, StructDeclarationSyntax structDeclaration, string menuName, string systemType)
        {
            var structName = structDeclaration.Identifier.Text;
            var singletonName = $"VFX{menuName}Singleton";
            var managerType = systemType == "Parentless" ? $"VFXManager<{structName}>" : $"VFXManagerParented<{structName}>";

            var source = $@"using Core.Scripts.Systems.External.Vfx.Base;
using Core.Scripts.Systems.External.Vfx.Types;
using Unity.Entities;

namespace Core.Scripts.Systems.External.Vfx
{{
    public struct {singletonName} : IComponentData
    {{
        public {managerType} Manager;
    }}
}}
";

            context.AddSource($"{singletonName}.g.cs", source);
        }
    }
}