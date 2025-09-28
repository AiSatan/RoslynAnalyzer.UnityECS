using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynCustomAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceEntityRemovalAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UnityRedDots004";
        private static readonly LocalizableString Title = "Entity not removed for component requiring removal";
        private static readonly LocalizableString MessageFormat = "Entity was not removed for component '{0}'";
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

            // Find the Entity parameter
            var entityParameter = methodDeclaration.ParameterList.Parameters.FirstOrDefault(p =>
            {
                var paramType = context.SemanticModel.GetTypeInfo(p.Type).Type as INamedTypeSymbol;
                return paramType != null && paramType.Name == "Entity" && paramType.ContainingNamespace?.ToDisplayString() == "Unity.Entities";
            });
            if (entityParameter == null) return;

            string entityName = entityParameter.Identifier.Text;

            // Check if the struct has DestroyEntity call with the entity
            bool hasDestroyEntity = HasDestroyEntityCall(containingType, methodDeclaration, entityName);

            // Analyze each parameter in the Execute method
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                // Get the parameter type symbol
                var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
                if (parameterType == null) continue;

                // Check if it's IComponentData and implements IEntityMustBeRemoved
                bool isComponentData = parameterType.AllInterfaces.Any(i => i.Name == "IComponentData");
                bool implementsMustBeRemoved = parameterType.AllInterfaces.Any(i => i.Name == "IEntityMustBeRemoved");
                if (!isComponentData || !implementsMustBeRemoved) continue;

                // If no DestroyEntity call, report diagnostic
                if (!hasDestroyEntity)
                {
                    var diagnostic = Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), parameter.Identifier.Text);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool HasDestroyEntityCall(TypeDeclarationSyntax containingType, MethodDeclarationSyntax executeMethod, string entityName)
        {
            // Get all method declarations in the struct
            var allMethods = containingType.DescendantNodes().OfType<MethodDeclarationSyntax>().ToDictionary(m => m.Identifier.Text);

            // Methods to check: Execute and methods called from Execute
            var methodsToCheck = new HashSet<string> { executeMethod.Identifier.Text };

            // Find methods called unconditionally from Execute
            if (executeMethod.Body != null)
            {
                var invocations = executeMethod.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    if (invocation.Expression is IdentifierNameSyntax identifier &&
                        allMethods.ContainsKey(identifier.Identifier.Text))
                    {
                        // Check if the invocation is unconditional (not inside if, while, etc.)
                        bool isUnconditional = !invocation.Ancestors().Any(a => a is IfStatementSyntax || a is WhileStatementSyntax || a is ForStatementSyntax || a is ForEachStatementSyntax || a is SwitchStatementSyntax);
                        if (isUnconditional)
                        {
                            methodsToCheck.Add(identifier.Identifier.Text);
                        }
                    }
                }
            }

            // Check each method in methodsToCheck
            foreach (var methodName in methodsToCheck)
            {
                if (allMethods.TryGetValue(methodName, out var method) && method.Body != null)
                {
                    var invocations = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
                    foreach (var invocation in invocations)
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                            memberAccess.Name.Identifier.Text == "DestroyEntity" &&
                            memberAccess.Expression is IdentifierNameSyntax identifier &&
                            identifier.Identifier.Text == "Ecb")
                        {
                            // Check if arguments contain the entity name
                            var args = invocation.ArgumentList.Arguments;
                            if (args.Count >= 2)
                            {
                                var secondArg = args[1].Expression;
                                if (secondArg is IdentifierNameSyntax argIdentifier && argIdentifier.Identifier.Text == entityName)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}