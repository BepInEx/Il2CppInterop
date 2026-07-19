using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Il2CppInterop.SourceGenerator;

internal sealed record TypeModel(
    string Namespace,
    string TypeName,
    string? Il2CppNamespace,
    string? Il2CppName,
    TypeKind TypeKind, // "class", "struct", or "interface"
    EquatableArray<MemberModel> Members,
    EquatableArray<string> FinalizerMethodNames,
    bool NeedsObjectPointerConstructor,
    Accessibility? DeclaredAccessibility,
    bool IsAbstract
)
{
    internal static TypeModel? FromSymbol(INamedTypeSymbol node, string? il2CppNamespace, string? il2CppName, CancellationToken ct)
    {

        var typeKind = node.TypeKind;

        switch (typeKind)
        {
            case TypeKind.Unknown:
            case TypeKind.Class when (node.BaseType?.SpecialType == SpecialType.System_Object):
                return null;
        }


        var members = new List<MemberModel>();
        var index = 0;

        if (typeKind == TypeKind.Struct)
        {
            foreach (var field in node.GetMembers().OfType<IFieldSymbol>())
            {
                ct.ThrowIfCancellationRequested();

                var attrs = field.GetAttributes();
                if (field.IsStatic && !attrs.Any(a => a.AttributeClass.IsType("Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"])))
                {
                    // Il2CppFieldAttribute is required for static fields, but not for instance fields.
                    continue;
                }

                members.Add(new MemberModel(
                    Name: field.Name,
                    Type: field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Kind: MemberKind.Il2Cpp,
                    Index: index++,
                    Accessibility: null,
                    IsStatic: field.IsStatic
                ));
            }
        }
        foreach (var property in node.GetMembers().OfType<IPropertySymbol>())
        {
            ct.ThrowIfCancellationRequested();

            var attrs = property.GetAttributes();
            var il2cpp = attrs.Any(a => a.AttributeClass.IsType("Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));
            var managed = attrs.Any(a => a.AttributeClass.IsType("ManagedFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));

            if (!il2cpp && !managed)
                continue;
            if (!property.IsPartialDefinition)
                continue;
            if (managed && property.IsStatic)
                continue;

            // DeclaredAccessibility defaults to Private when no modifier is written,
            // so we check the syntax directly to distinguish explicit "private" from omitted.
            var explicitAccess = property.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax(ct))
                .OfType<PropertyDeclarationSyntax>()
                .SelectMany(s => s.Modifiers)
                .Any(m => m.IsKind(SyntaxKind.PublicKeyword)
                          || m.IsKind(SyntaxKind.InternalKeyword)
                          || m.IsKind(SyntaxKind.PrivateKeyword)
                          || m.IsKind(SyntaxKind.ProtectedKeyword));

            members.Add(new MemberModel(
                Name: property.Name,
                Type: property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Kind: managed ? MemberKind.Managed : MemberKind.Il2Cpp,
                Index: index++,
                Accessibility: !explicitAccess ? null : property.DeclaredAccessibility,
                IsStatic: property.IsStatic));
        }

        IReadOnlyList<string> finalizerMethods = [];
        var needsObjectPointerConstructor = false;

        if (typeKind == TypeKind.Class)
        {
            finalizerMethods = [
                ..node.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m =>
                        !m.IsStatic &&
                        m.Parameters.IsEmpty &&
                        m.ReturnsVoid &&
                        m.GetAttributes().Any(a =>
                            a.AttributeClass.IsType("Il2CppFinalizerAttribute", ["Il2CppInterop", "Common", "Attributes"])))
                    .Select(m => m.Name)
            ];

            needsObjectPointerConstructor = !node.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.MethodKind == MethodKind.Constructor &&
                          m.Parameters is [{ Type: INamedTypeSymbol s }] &&
                          s.IsType("ObjectPointer", ["Il2CppInterop", "Common"]));
        }

        return new TypeModel(
            Namespace: node.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : node.ContainingNamespace.ToDisplayString(),
            TypeName: node.Name,
            Il2CppNamespace: il2CppNamespace,
            Il2CppName: il2CppName,
            TypeKind: typeKind,
            Members: new EquatableArray<MemberModel>(members),
            FinalizerMethodNames: new EquatableArray<string>(finalizerMethods),
            NeedsObjectPointerConstructor: needsObjectPointerConstructor,
            DeclaredAccessibility: node.DeclaredAccessibility,
            IsAbstract: node.IsAbstract);
    }

}

