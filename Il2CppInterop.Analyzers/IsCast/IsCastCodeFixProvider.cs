using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Il2CppInterop.Analyzers.IsCast
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IsCastCodeFixProvider)), Shared]
    public sealed class IsCastCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add TryCast";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(IsCastAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var isExpression = root!.FindToken(diagnosticSpan.Start).Parent!.AncestorsAndSelf().OfType<BinaryExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: cancellationToken => ReplaceWithTryCastAndPatternMatchingAsync(context.Document, isExpression, cancellationToken),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> ReplaceWithTryCastAndPatternMatchingAsync(Document document, BinaryExpressionSyntax isExpression, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var targetType = (TypeSyntax)isExpression.Right;

            var tryCastInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    isExpression.Left,
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("TryCast"))
                        .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(targetType.ToSeparatedSyntaxList()))))
                .WithArgumentList(SyntaxFactory.ArgumentList());

            editor.ReplaceNode(isExpression, isExpression.WithLeft(tryCastInvocation));
            return editor.GetChangedDocument();
        }
    }
}
