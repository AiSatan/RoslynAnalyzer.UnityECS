using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynCustomAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReadOnlyComponentAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "UnityRedDots001";
    private static readonly LocalizableString Title = "Modification of read-only component copy";

    private static readonly LocalizableString MessageFormat =
        "Variable '{0}' is a copy from a [ReadOnly] source and will not be saved";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat,
        "Correctness", DiagnosticSeverity.Warning, isEnabledByDefault: true);

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
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess?.Name.Identifier.Text != "TryGetComponent") return;

        var lookupSymbol = context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as IFieldSymbol;
        if (lookupSymbol == null ||
            !lookupSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "ReadOnlyAttribute"))
        {
            return;
        }

        var outArgument =
            invocation.ArgumentList.Arguments.FirstOrDefault(arg => arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword));
        var declaration = outArgument?.Expression as DeclarationExpressionSyntax;
        if (declaration == null) return;

        var designation = declaration.Designation as SingleVariableDesignationSyntax;
        if (designation == null) return;

        var componentVarSymbol = context.SemanticModel.GetDeclaredSymbol(designation);
        if (componentVarSymbol == null) return;

        // More robustly find the body of the containing method or local function
        var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        SyntaxNode? methodBody = containingMethod?.Body ?? (SyntaxNode?)containingMethod?.ExpressionBody;

        if (methodBody == null)
        {
            var containingLocalFunction = invocation.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
            methodBody = containingLocalFunction?.Body ?? (SyntaxNode?)containingLocalFunction?.ExpressionBody;
        }

        if (methodBody == null)
            return;

        var assignments = methodBody.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => (a.Left as MemberAccessExpressionSyntax)?.Expression.ToString() == componentVarSymbol.Name);

        foreach (var assignment in assignments)
        {
            // NEW LOGIC: Check if this modification is followed by a SetComponent call.
            bool isSaved = IsModificationSaved(methodBody, assignment, componentVarSymbol.Name);

            // Only report a diagnostic if the change is NOT saved.
            if (!isSaved)
            {
                var diagnostic = Diagnostic.Create(Rule, assignment.GetLocation(), componentVarSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private bool IsModificationSaved(SyntaxNode methodBody, AssignmentExpressionSyntax assignment,
        string componentVarName)
    {
        // Get all method calls that happen after the current assignment.
        var subsequentInvocations = methodBody.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.SpanStart > assignment.Span.End);

        foreach (var invocation in subsequentInvocations)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess?.Name.Identifier.Text != "SetComponent") continue;

            // Check if the last argument of SetComponent is our variable.
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                var lastArgument = invocation.ArgumentList.Arguments.Last();
                if (lastArgument.Expression.ToString() == componentVarName)
                {
                    // The change was saved. No warning needed for this assignment.
                    return true;
                }
            }
        }

        // We scanned the rest of the method and found no matching SetComponent call.
        return false;
    }
}