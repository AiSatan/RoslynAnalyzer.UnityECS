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

            // Collect components that require removal from parameters and attributes
            var componentsToCheck = new List<(INamedTypeSymbol type, Location location, string name)>();

            // From parameters
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
                if (parameterType == null) continue;

                INamedTypeSymbol componentType = null;
                if (parameterType.Name == "DynamicBuffer" && parameterType.ContainingNamespace?.ToDisplayString() == "Unity.Entities" && parameterType.TypeArguments.Length > 0)
                {
                    // For DynamicBuffer<T>, check T
                    componentType = parameterType.TypeArguments[0] as INamedTypeSymbol;
                }
                else
                {
                    componentType = parameterType;
                }

                if (componentType != null)
                {
                    bool isComponentData = componentType.AllInterfaces.Any(i => i.Name == "IComponentData" || i.Name == "IBufferElementData");
                    bool implementsMustBeRemoved = componentType.AllInterfaces.Any(i => i.Name == "IEntityMustBeRemoved");
                    if (isComponentData && implementsMustBeRemoved)
                    {
                        componentsToCheck.Add((componentType, parameter.Identifier.GetLocation(), parameter.Identifier.Text));
                    }
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
                            if (attrTypeSymbol != null && (attrTypeSymbol.AllInterfaces.Any(i => i.Name == "IComponentData") || attrTypeSymbol.AllInterfaces.Any(i => i.Name == "IBufferElementData")) && attrTypeSymbol.AllInterfaces.Any(i => i.Name == "IEntityMustBeRemoved"))
                            {
                                componentsToCheck.Add((attrTypeSymbol, typeofExpr.GetLocation(), attrTypeSymbol.Name));
                            }
                        }
                    }
                }
            }

            // Check if DestroyEntity is called
            bool hasDestroyEntity = HasDestroyEntityCall(containingType, methodDeclaration, entityName);

            // Report diagnostics for each component that requires removal
            foreach (var (type, location, name) in componentsToCheck)
            {
                if (!hasDestroyEntity)
                {
                    var diagnostic = Diagnostic.Create(Rule, location, name);
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