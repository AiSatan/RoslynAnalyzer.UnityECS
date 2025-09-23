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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnityDotsAnalyzersCodeFixProvider)), Shared]
public class UnityDotsAnalyzersCodeFixProvider : CodeFixProvider
{
    // This connects the fix to the warning from our analyzer
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ReadOnlyComponentAnalyzer.DiagnosticId);

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the assignment expression that triggered the warning (e.g., "target.LastSeenTime = ...")
        var assignmentNode = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<AssignmentExpressionSyntax>().First();

        if(assignmentNode == null)
            return;
        
        // Register a code action that will invoke the fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add Ecb.SetComponent to save changes",
                createChangedDocument: c => AddSetComponentCallAsync(context.Document, assignmentNode, c),
                equivalenceKey: "Add Ecb.SetComponent"),
            diagnostic);
    }

    private async Task<Document> AddSetComponentCallAsync(Document document, AssignmentExpressionSyntax assignmentExpr, CancellationToken cancellationToken)
    {
        // Get the variable name being modified (e.g., "target")
        var componentVarName = (assignmentExpr.Left as MemberAccessExpressionSyntax)?.Expression.ToString();
        if (string.IsNullOrEmpty(componentVarName)) return document;

        // Find the full statement (the line ending with the semicolon)
        var statementToInsertAfter = assignmentExpr.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        
        // --- This is a simplified assumption for the example ---
        // A more robust analyzer would scan the method for the actual variable names.
        var ecbName = "Ecb";
        var indexName = "index";
        var entityName = "entity";
        // ---------------------------------------------------------
        
        // Create the new code line: "Ecb.SetComponent(index, entity, target);"
        var newStatement = SyntaxFactory.ParseStatement($"{ecbName}.SetComponent({indexName}, {entityName}, {componentVarName});")
            .WithLeadingTrivia(statementToInsertAfter?.GetLeadingTrivia()) // Keep the same indentation
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed) // Add a new line after
            .WithTrailingTrivia(statementToInsertAfter?.GetTrailingTrivia()); // This is the fix

        // Get an editor to modify the document
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        
        // Insert the new statement after the line with the modification
        if (statementToInsertAfter != null) 
            editor.InsertAfter(statementToInsertAfter, newStatement);

        return editor.GetChangedDocument();
    }
    
    public sealed override FixAllProvider GetFixAllProvider()
    {
        // This enables the "Fix all occurrences in..." feature.
        return WellKnownFixAllProviders.BatchFixer;
    }
}