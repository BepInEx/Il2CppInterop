using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Il2CppInterop.Analyzers.AsCast;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsCastCodeFixProvider)), Shared]
public sealed class AsCastCodeFixProvider : CodeFixProvider
{
    private const string Title = "Replace with TryCast";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AsCastAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var asExpression = root!.FindToken(diagnosticSpan.Start).Parent!.AncestorsAndSelf().OfType<BinaryExpressionSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => ReplaceWithTryCastAsync(context.Document, asExpression, cancellationToken),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithTryCastAsync(Document document, BinaryExpressionSyntax asExpression, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var targetType = (TypeSyntax)asExpression.Right;

        var tryCastInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    asExpression.Left,
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("TryCast"))
                        .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(targetType.ToSeparatedSyntaxList()))))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        editor.ReplaceNode(asExpression, tryCastInvocation);
        return editor.GetChangedDocument();
    }

}