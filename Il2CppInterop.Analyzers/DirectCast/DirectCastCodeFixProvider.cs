using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Il2CppInterop.Analyzers.DirectCast
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DirectCastCodeFixProvider)), Shared]
    public class DirectCastCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Replace with Cast";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DirectCastAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var castExpression = root!.FindToken(diagnosticSpan.Start).Parent!.AncestorsAndSelf().OfType<CastExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: cancellationToken => ReplaceWithCastAsync(context.Document, castExpression, cancellationToken),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> ReplaceWithCastAsync(Document document, CastExpressionSyntax castExpression, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var tryCastInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    castExpression.Expression,
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Cast"))
                        .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(castExpression.Type)))))
                .WithArgumentList(SyntaxFactory.ArgumentList());

            editor.ReplaceNode(castExpression, tryCastInvocation);
            return editor.GetChangedDocument();
        }
    }
}
