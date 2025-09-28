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

            // Collect components that require removal from parameters and attributes
            var componentsToCheck = new List<(INamedTypeSymbol type, Location location, string name)>();

            // From parameters
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
                if (parameterType == null) continue;

                bool isComponentData = parameterType.AllInterfaces.Any(i => i.Name == "IComponentData");
                bool implementsMustBeRemoved = parameterType.AllInterfaces.Any(i => i.Name == "IComponentMustBeRemoved");
                if (isComponentData && implementsMustBeRemoved)
                {
                    componentsToCheck.Add((parameterType, parameter.Identifier.GetLocation(), parameter.Identifier.Text));
                }
            }

            // From any attribute that uses typeof(Component), except WithNone
            var attributes = containingType.AttributeLists.SelectMany(al => al.Attributes);
            foreach (var attribute in attributes)
            {
                var attributeName = attribute.Name.ToString();
                if (attributeName.StartsWith("With") && attributeName != "WithNone")
                {
                    foreach (var arg in attribute.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>())
                    {
                        if (arg.Expression is TypeOfExpressionSyntax typeofExpr)
                        {
                            var attrTypeSymbol = context.SemanticModel.GetTypeInfo(typeofExpr.Type).Type as INamedTypeSymbol;
                            if (attrTypeSymbol != null && attrTypeSymbol.AllInterfaces.Any(i => i.Name == "IComponentData") && attrTypeSymbol.AllInterfaces.Any(i => i.Name == "IComponentMustBeRemoved"))
                            {
                                componentsToCheck.Add((attrTypeSymbol, typeofExpr.GetLocation(), attrTypeSymbol.Name));
                            }
                        }
                    }
                }
            }

            // Check and report diagnostics for each component
            foreach (var (type, location, name) in componentsToCheck)
            {
                string componentTypeName = type.ToDisplayString();
                bool hasRemoveComponent = HasRemoveComponentCall(context, containingType, methodDeclaration, componentTypeName);

                if (!hasRemoveComponent)
                {
                    var diagnostic = Diagnostic.Create(Rule, location, name);
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