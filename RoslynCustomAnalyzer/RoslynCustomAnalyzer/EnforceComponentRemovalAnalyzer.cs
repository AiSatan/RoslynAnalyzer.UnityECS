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
    public class EnforceComponentRemovalAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UnityRedDots005";
        private static readonly LocalizableString Title = "Component not removed for component requiring removal";
        private static readonly LocalizableString MessageFormat = "Component '{0}' was not removed";
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
                // Get the parameter type symbol
                var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
                if (parameterType == null) continue;

                // Check if it's IComponentData and implements IComponentMustBeRemoved
                bool isComponentData = parameterType.AllInterfaces.Any(i => i.Name == "IComponentData");
                bool implementsMustBeRemoved = parameterType.AllInterfaces.Any(i => i.Name == "IComponentMustBeRemoved");
                if (!isComponentData || !implementsMustBeRemoved) continue;

                string componentTypeName = parameterType.ToDisplayString();

                // Check if the struct has RemoveComponent call with the component type
                bool hasRemoveComponent = HasRemoveComponentCall(context, containingType, methodDeclaration, componentTypeName);

                // If no RemoveComponent call, report diagnostic
                if (!hasRemoveComponent)
                {
                    var diagnostic = Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), parameter.Identifier.Text);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool HasRemoveComponentCall(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax containingType, MethodDeclarationSyntax executeMethod, string componentTypeName)
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

            // Find methods where AddComponent is called with this type
            var excludedMethods = new HashSet<string>();
            foreach (var method in allMethods.Values)
            {
                if (method.Body != null)
                {
                    var invocations = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
                    foreach (var invocation in invocations)
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                            memberAccess.Name.Identifier.Text == "AddComponent" &&
                            memberAccess.Expression is IdentifierNameSyntax identifier &&
                            identifier.Identifier.Text == "Ecb")
                        {
                            // Check if the type argument is the component type
                            var typeArgs = invocation.DescendantNodes().OfType<TypeArgumentListSyntax>().FirstOrDefault();
                            if (typeArgs != null && typeArgs.Arguments.Count > 0)
                            {
                                var typeArg = typeArgs.Arguments[0];
                                var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;
                                if (typeSymbol != null && typeSymbol.ToDisplayString() == componentTypeName)
                                {
                                    excludedMethods.Add(method.Identifier.Text);
                                }
                            }
                        }
                    }
                }
            }

            // Check each method in methodsToCheck, excluding excludedMethods
            foreach (var methodName in methodsToCheck)
            {
                if (excludedMethods.Contains(methodName)) continue;

                if (allMethods.TryGetValue(methodName, out var method) && method.Body != null)
                {
                    var invocations = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
                    foreach (var invocation in invocations)
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                            memberAccess.Name.Identifier.Text == "RemoveComponent" &&
                            memberAccess.Expression is IdentifierNameSyntax identifier &&
                            identifier.Identifier.Text == "Ecb")
                        {
                            // Check if the type argument is the component type
                            var typeArgs = invocation.DescendantNodes().OfType<TypeArgumentListSyntax>().FirstOrDefault();
                            if (typeArgs != null && typeArgs.Arguments.Count > 0)
                            {
                                var typeArg = typeArgs.Arguments[0];
                                var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;
                                if (typeSymbol != null && typeSymbol.ToDisplayString() == componentTypeName)
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