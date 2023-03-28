﻿using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.Analyzers.IsCast;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IsCastAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "Interop0003";

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
        context.RegisterSyntaxNodeAction(AnalyzeIs, SyntaxKind.IsExpression);
    }

    private static void AnalyzeIs(SyntaxNodeAnalysisContext context)
    {
        var isExpression = (BinaryExpressionSyntax)context.Node;
        if (!isExpression.IsKind(SyntaxKind.IsExpression)) return;

        var sourceType = context.SemanticModel.GetTypeInfo(isExpression.Left).Type;
        if (sourceType == null || !Utilities.IsIl2CppObject(context, sourceType)) return;

        var targetTypeSyntax = Utilities.GetTypeFromPattern(isExpression.Right);
        if (targetTypeSyntax == null) return;

        var targetType = context.SemanticModel.GetTypeInfo(targetTypeSyntax).Type;
        if (targetType == null || !Utilities.IsIl2CppObject(context, targetType)) return;

        if (targetType.Equals(sourceType, SymbolEqualityComparer.Default)) return;

        var diagnostic = Diagnostic.Create(s_rule, isExpression.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
