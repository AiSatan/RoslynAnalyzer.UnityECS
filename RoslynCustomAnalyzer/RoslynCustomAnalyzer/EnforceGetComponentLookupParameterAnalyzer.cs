using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RoslynCustomAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceGetComponentLookupParameterAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UnityRedDots002";
        private static readonly LocalizableString Title = "Lookup method missing read-only parameter";
        private static readonly LocalizableString MessageFormat = "SystemAPI.GetComponentLookup and SystemAPI.GetBufferLookup must explicitly specify the read-only parameter (true or false)";
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, "Correctness", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check if the method call is a member access (e.g., "object.Method()")
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess)) return;

            // Check if the method name is "GetComponentLookup" or "GetBufferLookup"
            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName != "GetComponentLookup" && methodName != "GetBufferLookup") return;
            
            // Use the semantic model to robustly check if the call is on Unity.Entities.SystemAPI
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol == null || methodSymbol.ContainingType?.ToDisplayString() != "Unity.Entities.SystemAPI")
            {
                return;
            }

            // The rule: trigger a warning if there are no arguments
            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                // This is the fix: The location spans from the start of the method name
                // to the end of the argument list, which matches the test's expectation.
                var location = Location.Create(
                    invocation.SyntaxTree,
                    TextSpan.FromBounds(memberAccess.Name.Span.Start, invocation.ArgumentList.Span.End)
                );
                var diagnostic = Diagnostic.Create(Rule, location);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

