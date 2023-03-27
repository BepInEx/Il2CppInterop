using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.Analyzers.DirectCast;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectCastAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "Interop0001";

    private static readonly LocalizableString s_title = "Cast to Il2CppSystem.Object detected";
    private static readonly LocalizableString s_messageFormat = "Il2Cpp objects must be casted using .Cast or .TryCast";
    private static readonly LocalizableString s_description = "Casting an object inheriting from Il2CppSystem.Object to a type also inheriting from Il2CppSystem.Object should be done with Cast or TryCast.";
    private const string Category = "Casting";

    private static readonly DiagnosticDescriptor s_rule = new(DiagnosticId, s_title, s_messageFormat, Category, DiagnosticSeverity.Warning, true, s_description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
    }

    private static void AnalyzeCast(SyntaxNodeAnalysisContext context)
    {
        var castExpression = (CastExpressionSyntax)context.Node;

        var targetType = context.SemanticModel.GetTypeInfo(castExpression.Type).Type;
        if (targetType == null || !Utilities.IsIl2CppObject(context, targetType)) return;

        var sourceType = context.SemanticModel.GetTypeInfo(castExpression.Expression).Type;
        if (sourceType == null || !Utilities.IsIl2CppObject(context, sourceType)) return;

        if (targetType.Equals(sourceType, SymbolEqualityComparer.Default)) return;

        var diagnostic = Diagnostic.Create(s_rule, castExpression.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}