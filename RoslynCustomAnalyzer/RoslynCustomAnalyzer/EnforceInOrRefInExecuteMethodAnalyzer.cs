using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynCustomAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceInOrRefInExecuteMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UnityRedDots003";
        private static readonly LocalizableString Title = "Missing 'in' or 'ref' modifier on Execute method parameter";
        private static readonly LocalizableString MessageFormat = "Parameter '{0}' in Execute method must have 'in' or 'ref' modifier";
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, "Correctness", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Check if the method name is "Execute"
            if (methodDeclaration.Identifier.Text != "Execute") return;

            // Check if the containing type is a struct
            var containingType = methodDeclaration.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (containingType == null || containingType.Keyword.Text != "struct") return;

            // Check if the struct implements IJobEntity
            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(containingType) as INamedTypeSymbol;
            if (typeSymbol == null || !typeSymbol.AllInterfaces.Any(i => i.Name == "IJobEntity")) return;

            // Analyze each parameter in the Execute method
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                // Skip parameters that already have 'in' or 'ref'
                if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.InKeyword) || m.IsKind(SyntaxKind.RefKeyword)))
                    continue;

                // Report diagnostic for parameters without 'in' or 'ref'
                var diagnostic = Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), parameter.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}