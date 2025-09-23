using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace RoslynCustomAnalyzer;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnforceGetComponentLookupParameterCodeFixProvider)), Shared]
public class EnforceGetComponentLookupParameterCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EnforceGetComponentLookupParameterAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the InvocationExpressionSyntax node that triggered the diagnostic.
        var invocationExpr = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

        if (invocationExpr == null)
            return;
        
        // Create two separate code actions: one for 'true' and one for 'false'.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'true' for read-only access",
                createChangedDocument: c => AddArgumentAsync(context.Document, invocationExpr, true, c),
                equivalenceKey: "Add 'true'"),
            diagnostic);
            
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'false' for read-write access",
                createChangedDocument: c => AddArgumentAsync(context.Document, invocationExpr, false, c),
                equivalenceKey: "Add 'false'"),
            diagnostic);
    }

    private async Task<Document> AddArgumentAsync(Document document, InvocationExpressionSyntax invocationExpr, bool value, CancellationToken cancellationToken)
    {
        // Create the new argument syntax (either 'true' or 'false').
        var literalExpression = value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression;
        var newArgument = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(literalExpression));

        // Create the new argument list with the added argument.
        var newArgumentList = invocationExpr.ArgumentList.AddArguments(newArgument);
        
        // Replace the old invocation node with the new one.
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        editor.ReplaceNode(invocationExpr, invocationExpr.WithArgumentList(newArgumentList));
        
        return editor.GetChangedDocument();
    }
}