using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynCustomAnalyzer.SourceGenerator
{
    [Generator]
    public partial class VfxSystemGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                return;

            GenerateManagedResources(context, receiver.IdentifiedStructs);

            foreach (var (structDeclaration, attribute) in receiver.IdentifiedStructs)
            {
                var semanticModel = context.Compilation.GetSemanticModel(structDeclaration.SyntaxTree);
                var symbol = semanticModel.GetDeclaredSymbol(structDeclaration) as ITypeSymbol;
                var isParented = symbol?.AllInterfaces.Any(i => i.Name == "IKillableVFX");
                var systemType = isParented == true ? "Parented" : "Parentless";

                var menuName = attribute.ConstructorArguments[0].Value?.ToString() ?? "ERROR";
                int.TryParse(attribute.ConstructorArguments[1].Value?.ToString() ?? "-1", out var capacity );

                GenerateSingleton(context, structDeclaration, menuName, systemType);
                GenerateSystem(context, structDeclaration, menuName, systemType, capacity);
            }
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<(StructDeclarationSyntax, AttributeData)> IdentifiedStructs { get; } = new List<(StructDeclarationSyntax, AttributeData)>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is StructDeclarationSyntax structDeclarationSyntax)
                {
                    var symbol = context.SemanticModel.GetDeclaredSymbol(structDeclarationSyntax);
                    if (symbol is ITypeSymbol typeSymbol)
                    {
                        var attribute = typeSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass.ToDisplayString() == "Core.Scripts.Systems.External.Vfx.Base.GenerateVFXSystemAttribute");
                        if (attribute != null)
                        {
                            IdentifiedStructs.Add((structDeclarationSyntax, attribute));
                        }
                    }
                }
            }
        }
    }
}