using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectedTypeAnalyzer : DiagnosticAnalyzer
{
    #region Diagnostic Descriptors

    public static DiagnosticDescriptor MustBePartial { get; } = new(
        id: "IL2CPP0001",
        title: "Injected type must be partial",
        messageFormat: "Type '{0}' is marked with [InjectedType] but is not declared as partial. Add the 'partial' modifier so the source generator can augment it.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types decorated with [InjectedTypeAttribute] must be partial. " +
                     "The Il2CppTypeGenerator source generator needs to add members to the type, " +
                     "which requires the partial keyword.",
        helpLinkUri: "https://github.com/BepInEx/Il2CppInterop");

    public static DiagnosticDescriptor MustInheritIl2CppObject { get; } = new(
        id: "IL2CPP0002",
        title: "Injected class must inherit from Il2CppSystem.Object",
        messageFormat: "Class '{0}' is marked with [InjectedType] but does not inherit from Il2CppSystem.Object",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CannotHaveStaticConstructor { get; } = new(
        id: "IL2CPP0003",
        title: "Injected type cannot have a static constructor",
        messageFormat: "Type '{0}' is marked with [InjectedType] but has a static constructor. Remove it, as Il2CppInternals handles static initialization.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CannotOverrideIl2CppFinalize { get; } = new(
        id: "IL2CPP0004",
        title: "Injected type should not manually override Il2CppFinalize",
        messageFormat: "Type '{0}' manually overrides Il2CppFinalize. Use [Il2CppFinalizer] on a method instead.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CannotHaveManagedFieldOnStructOrInterface { get; } = new(
        id: "IL2CPP0005",
        title: "Structs and interfaces cannot have [ManagedField] properties",
        messageFormat: "Type '{0}' is a {1} and cannot have properties marked with [ManagedField]",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CannotHaveIl2CppFieldOnStructOrInterface { get; } = new(
        id: "IL2CPP0006",
        title: "Structs and interfaces cannot have instance properties marked with [Il2CppField]",
        messageFormat: "Type '{0}' is a {1} and cannot have instance properties marked with [Il2CppField]",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ManagedFieldMustBeInstance { get; } = new(
        id: "IL2CPP0007",
        title: "Properties marked with [ManagedField] must be instance members",
        messageFormat: "Property '{0}' is marked with [ManagedField] but is static. [ManagedField] properties must be instance members.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor FieldPropertyMustBePartial { get; } = new(
        id: "IL2CPP0008",
        title: "Properties marked with [ManagedField] or [Il2CppField] must be partial",
        messageFormat: "Property '{0}' is marked with [{1}] but is not partial. Add the 'partial' modifier.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ShouldNotHaveUninjectedInstanceFields { get; } = new(
        id: "IL2CPP0009",
        title: "Injected types should not have uninjected instance fields",
        messageFormat: "Type '{0}' has instance field '{1}' which is not managed by Il2CppInterop. Use [ManagedField] or [Il2CppField] properties instead.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor Il2CppFinalizerMustHaveNoParameters { get; } = new(
        id: "IL2CPP0010",
        title: "Methods annotated with [Il2CppFinalizer] should have no parameters",
        messageFormat: "Method '{0}' is annotated with [Il2CppFinalizer] but has parameters. The finalizer must be parameterless.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor Il2CppFinalizerShouldReturnVoid { get; } = new(
        id: "IL2CPP0011",
        title: "Methods annotated with [Il2CppFinalizer] should return nothing",
        messageFormat: "Method '{0}' is annotated with [Il2CppFinalizer] but does not return void.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OnlyObjectPointerConstructorCanCallBase { get; } = new(
        id: "IL2CPP0012",
        title: "Only the ObjectPointer constructor can call a base constructor",
        messageFormat: "Constructor in type '{0}' calls a base constructor, but only the ObjectPointer constructor is allowed to do so.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor Il2CppFieldCannotBeStatic { get; } = new(
        id: "IL2CPP0013",
        title: "Static fields cannot be annotated with [Il2CppField]",
        messageFormat: "Field '{0}' is static and cannot be annotated with [Il2CppField].",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InstanceFieldsInClassesCannotBeInjected { get; } = new(
        id: "IL2CPP0014",
        title: "Instance fields in classes cannot be annotated with [Il2CppField]",
        messageFormat: "Field '{0}' in class '{1}' is an instance member annotated with [Il2CppField]. [Il2CppField] is only valid on properties in classes.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        MustBePartial,
        MustInheritIl2CppObject,
        CannotHaveStaticConstructor,
        CannotOverrideIl2CppFinalize,
        CannotHaveManagedFieldOnStructOrInterface,
        CannotHaveIl2CppFieldOnStructOrInterface,
        ManagedFieldMustBeInstance,
        FieldPropertyMustBePartial,
        ShouldNotHaveUninjectedInstanceFields,
        Il2CppFinalizerMustHaveNoParameters,
        Il2CppFinalizerShouldReturnVoid,
        OnlyObjectPointerConstructorCanCallBase,
        Il2CppFieldCannotBeStatic,
        InstanceFieldsInClassesCannotBeInjected,
    ];

    #endregion

    #region Registration

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeTypeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    #endregion

    #region Analysis

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var tds = (TypeDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(tds, context.CancellationToken);
        if (symbol is null)
            return;

        // Gate everything behind the attribute — no [InjectedType], no diagnostics
        if (!symbol.HasAttribute("InjectedTypeAttribute", ["Il2CppInterop", "Common", "Attributes"]))
            return;

        CheckMustBePartial(context, tds, symbol);
        CheckMustInheritIl2CppObject(context, tds, symbol);
        CheckNoStaticConstructor(context, tds, symbol);
        CheckNoIl2CppFinalizeOverride(context, tds, symbol);
        CheckNoManagedFieldOnStructOrInterface(context, tds, symbol);
        CheckNoIl2CppFieldOnStructOrInterface(context, tds, symbol);
        CheckManagedFieldMustBeInstance(context, tds);
        CheckFieldPropertiesMustBePartial(context, tds);
        CheckNoUninjectedInstanceMembers(context, tds, symbol);
        CheckIl2CppFinalizerMethods(context, tds);
        CheckConstructorsDoNotCallBase(context, tds, symbol);
        CheckIl2CppFieldOnStaticProperties(context, tds);
        CheckIl2CppFieldOnInstanceFieldsInClass(context, tds, symbol);
    }

    #endregion

    #region Helpers

    private static bool HasAttribute(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax prop,
        string attributeName,
        ReadOnlySpan<string> attributeNamespace)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(prop, context.CancellationToken);
        return symbol?.HasAttribute(attributeName, attributeNamespace) ?? false;
    }

    #endregion

    #region Checks

    // IL2CPP0001
    private static void CheckMustBePartial(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds.Modifiers.Any(SyntaxKind.PartialKeyword))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            MustBePartial,
            tds.Identifier.GetLocation(),
            symbol.Name));
    }

    // IL2CPP0002
    private static void CheckMustInheritIl2CppObject(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not ClassDeclarationSyntax || symbol.TypeKind != TypeKind.Class)
            return;

        var baseType = symbol.BaseType;

        var isSystemObject = baseType is null || baseType.IsType("Object", ["System"]);

        var inheritsIl2CppObject = false;
        var current = baseType;
        while (current is not null)
        {
            if (current.IsType("Object", ["Il2CppSystem"]))
            {
                inheritsIl2CppObject = true;
                break;
            }
            current = current.BaseType;
        }

        if (isSystemObject || !inheritsIl2CppObject)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MustInheritIl2CppObject,
                tds.Identifier.GetLocation(),
                symbol.Name));
        }
    }

    // IL2CPP0003
    private static void CheckNoStaticConstructor(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        var staticCtor = tds.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.Modifiers.Any(SyntaxKind.StaticKeyword));

        if (staticCtor is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            CannotHaveStaticConstructor,
            staticCtor.Identifier.GetLocation(),
            symbol.Name));
    }

    // IL2CPP0004
    private static void CheckNoIl2CppFinalizeOverride(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        var finalizeOverride = tds.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
                m.Identifier.Text == "Il2CppFinalize" &&
                m.Modifiers.Any(SyntaxKind.OverrideKeyword));

        if (finalizeOverride is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            CannotOverrideIl2CppFinalize,
            finalizeOverride.Identifier.GetLocation(),
            symbol.Name));
    }

    // IL2CPP0005
    private static void CheckNoManagedFieldOnStructOrInterface(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not (StructDeclarationSyntax or InterfaceDeclarationSyntax))
            return;

        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => HasAttribute(context, p, "ManagedFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CannotHaveManagedFieldOnStructOrInterface,
                prop.Identifier.GetLocation(),
                symbol.Name,
                symbol.TypeKind == TypeKind.Struct ? "struct" : "interface"));
        }
    }

    // IL2CPP0006
    private static void CheckNoIl2CppFieldOnStructOrInterface(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not (StructDeclarationSyntax or InterfaceDeclarationSyntax))
            return;

        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p =>
                !p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                HasAttribute(context, p, "Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CannotHaveIl2CppFieldOnStructOrInterface,
                prop.Identifier.GetLocation(),
                symbol.Name,
                symbol.TypeKind == TypeKind.Struct ? "struct" : "interface"));
        }
    }

    // IL2CPP0007
    private static void CheckManagedFieldMustBeInstance(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds)
    {
        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p =>
                p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                HasAttribute(context, p, "ManagedFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ManagedFieldMustBeInstance,
                prop.Identifier.GetLocation(),
                prop.Identifier.Text));
        }
    }

    // IL2CPP0008
    private static void CheckFieldPropertiesMustBePartial(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds)
    {
        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => !p.Modifiers.Any(SyntaxKind.PartialKeyword));

        foreach (var prop in offending)
        {
            var hasManagedField = HasAttribute(context, prop, "ManagedFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]);
            var hasIl2CppField = HasAttribute(context, prop, "Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]);

            if (!hasManagedField && !hasIl2CppField)
                continue;

            if (hasManagedField)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    FieldPropertyMustBePartial,
                    prop.Identifier.GetLocation(),
                    prop.Identifier.Text,
                    "ManagedField"));
            }

            if (hasIl2CppField)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    FieldPropertyMustBePartial,
                    prop.Identifier.GetLocation(),
                    prop.Identifier.Text,
                    "Il2CppField"));
            }
        }
    }

    // IL2CPP0009
    private static void CheckNoUninjectedInstanceMembers(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        // Instance fields in structs are automatically injected, so we don't need to check for them.
        if (tds is not StructDeclarationSyntax)
        {
            foreach (var field in tds.Members.OfType<FieldDeclarationSyntax>())
            {
                if (field.Modifiers.Any(SyntaxKind.StaticKeyword))
                    continue;

                foreach (var variable in field.Declaration.Variables)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ShouldNotHaveUninjectedInstanceFields,
                        variable.Identifier.GetLocation(),
                        symbol.Name,
                        variable.Identifier.Text));
                }
            }
        }
    }

    // IL2CPP0010 + IL2CPP0011
    private static void CheckIl2CppFinalizerMethods(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds)
    {
        var finalizerMethods = tds.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m =>
            {
                var sym = context.SemanticModel.GetDeclaredSymbol(m, context.CancellationToken);
                return sym?.HasAttribute("Il2CppFinalizerAttribute", ["Il2CppInterop", "Common", "Attributes"]) ?? false;
            });

        foreach (var method in finalizerMethods)
        {
            if (method.ParameterList.Parameters.Count > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Il2CppFinalizerMustHaveNoParameters,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text));
            }
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
            if (methodSymbol?.ReturnType.SpecialType != SpecialType.System_Void)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Il2CppFinalizerShouldReturnVoid,
                    method.ReturnType.GetLocation(),
                    method.Identifier.Text));
            }
        }
    }

    // IL2CPP0012
    private static void CheckConstructorsDoNotCallBase(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        var offending = tds.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c =>
                c.Initializer is { ThisOrBaseKeyword.RawKind: (int)SyntaxKind.BaseKeyword } &&
                !IsObjectPointerConstructor(context, c));

        foreach (var ctor in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OnlyObjectPointerConstructorCanCallBase,
                ctor.Initializer!.GetLocation(),
                symbol.Name));
        }
    }

    private static bool IsObjectPointerConstructor(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax ctor)
    {
        if (ctor.ParameterList.Parameters.Count != 1)
            return false;

        var paramSyntax = ctor.ParameterList.Parameters[0];
        var paramSymbol = context.SemanticModel.GetDeclaredSymbol(paramSyntax, context.CancellationToken);
        return (paramSymbol?.Type as INamedTypeSymbol).IsType("ObjectPointer", ["Il2CppInterop", "Common"]);
    }

    // IL2CPP0013
    private static void CheckIl2CppFieldOnStaticProperties(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds)
    {
        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p =>
                p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                HasAttribute(context, p, "Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Il2CppFieldCannotBeStatic,
                prop.Identifier.GetLocation(),
                prop.Identifier.Text));
        }
    }

    // IL2CPP0014
    private static void CheckIl2CppFieldOnInstanceFieldsInClass(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not ClassDeclarationSyntax || symbol.TypeKind != TypeKind.Class)
            return;

        foreach (var field in tds.Members.OfType<FieldDeclarationSyntax>()
                     .Where(f => !f.Modifiers.Any(SyntaxKind.StaticKeyword)))
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
                if (fieldSymbol?.HasAttribute("Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]) != true)
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    InstanceFieldsInClassesCannotBeInjected,
                    variable.Identifier.GetLocation(),
                    variable.Identifier.Text,
                    symbol.Name));
            }
        }
    }

    #endregion
}
