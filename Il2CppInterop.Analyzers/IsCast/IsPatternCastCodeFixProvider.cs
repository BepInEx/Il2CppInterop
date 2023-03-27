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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IsPatternCastCodeFixProvider)), Shared]
    public sealed class IsPatternCastCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add TryCast";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(IsPatternCastAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var isExpression = root!.FindToken(diagnosticSpan.Start).Parent!.AncestorsAndSelf().OfType<IsPatternExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: cancellationToken => ReplaceWithTryCastAndPatternMatchingAsync(context.Document, isExpression, cancellationToken),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> ReplaceWithTryCastAndPatternMatchingAsync(Document document, IsPatternExpressionSyntax isExpression, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            TypeSyntax targetType;
            switch (isExpression.Pattern)
            {
                case DeclarationPatternSyntax declaration:
                    targetType = declaration.Type;
                    break;
                case RecursivePatternSyntax recursive:
                    targetType = recursive.Type;
                    break;
                default:
                    return editor.OriginalDocument;
            }

            var tryCastInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    isExpression.Expression,
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("TryCast"))
                        .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(targetType.ToSeparatedSyntaxList()))))
                .WithArgumentList(SyntaxFactory.ArgumentList());

            editor.ReplaceNode(isExpression, isExpression.WithExpression(tryCastInvocation));
            return editor.GetChangedDocument();
        }
    }
}
