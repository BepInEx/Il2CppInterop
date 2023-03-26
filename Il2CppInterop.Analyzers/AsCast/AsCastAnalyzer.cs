using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.Analyzers.AsCast
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsCastAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "Interop0002";

        private static readonly LocalizableString s_title = "Cast to Il2CppSystem.Object detected";
        private static readonly LocalizableString s_messageFormat = "Il2Cpp objects must be casted using .Cast or .TryCast";
        private static readonly LocalizableString s_description = "This analyzer warns when casting an object inheriting from Il2CppSystem.Object using a standard cast, and suggests using .Cast or .TryCast instead.";
        private const string Category = "Casting";

        private static readonly DiagnosticDescriptor s_rule = new(DiagnosticId, s_title, s_messageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: s_description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.AsExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var asExpression = (BinaryExpressionSyntax)context.Node;

            var targetType = context.SemanticModel.GetTypeInfo(asExpression.Right).Type;
            if (targetType == null || !IsIl2CppObject(context, targetType)) return;

            var sourceType = context.SemanticModel.GetTypeInfo(asExpression.Left).Type;
            if (sourceType == null || !IsIl2CppObject(context, sourceType)) return;

            var diagnostic = Diagnostic.Create(s_rule, asExpression.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }


        private bool IsIl2CppObject(SyntaxNodeAnalysisContext context, ITypeSymbol typeSymbol)
        {
            var il2CppObjectType = context.Compilation.GetTypeByMetadataName("Il2CppSystem.Object");
            return typeSymbol.Equals(il2CppObjectType, SymbolEqualityComparer.Default) ||
                   (typeSymbol.BaseType != null && typeSymbol.BaseType.Equals(il2CppObjectType, SymbolEqualityComparer.Default));
        }
    }
}
