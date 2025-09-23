using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoslynCustomAnalyzer.Tests.Helpers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddFalseCodeFixProvider)), Shared]
public class AddFalseCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [EnforceGetComponentLookupParameterAnalyzer.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var invocation = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invocation == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'false' (writable)",
                createChangedDocument: c => AddArgumentAsync(context.Document, invocation, false, c),
                equivalenceKey: "Add 'false'"),
            diagnostic);
    }

    private async Task<Document> AddArgumentAsync(Document document, InvocationExpressionSyntax invocation, bool value,
        System.Threading.CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        var newInvocation = invocation.AddArgumentListArguments(
            Argument(LiteralExpression(value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression))
        );
        editor.ReplaceNode(invocation, newInvocation);
        return editor.GetChangedDocument();
    }
}