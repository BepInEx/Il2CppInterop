using System.Diagnostics;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Contexts;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class TypeRewriteContext
{
    public enum TypeSpecifics
    {
        NotComputed,
        Computing,
        ReferenceType,
        BlittableStruct,
        NonBlittableStruct
    }

    public readonly AssemblyRewriteContext AssemblyContext;

    private readonly Dictionary<FieldDefinition, FieldRewriteContext> myFieldContexts = new();
    private readonly Dictionary<MethodDefinition, MethodRewriteContext> myMethodContexts = new();
    private readonly Dictionary<string, MethodRewriteContext> myMethodContextsByName = new();
    public readonly TypeDefinition NewType;

    public readonly bool OriginalNameWasObfuscated;
#nullable disable
    // OriginalType is null for unstripped types, but we don't want to warn anywhere,
    // including in the constructor, so we disable all null tracking for this field.
    public readonly TypeDefinition OriginalType;
#nullable enable

    public TypeSpecifics ComputedTypeSpecifics;

    public TypeRewriteContext(AssemblyRewriteContext assemblyContext, TypeDefinition? originalType,
        TypeDefinition newType)
    {
        AssemblyContext = assemblyContext ?? throw new ArgumentNullException(nameof(assemblyContext));
        OriginalType = originalType;
        NewType = newType ?? throw new ArgumentNullException(nameof(newType));

        if (OriginalType == null) return;

        OriginalNameWasObfuscated = OriginalType.Name != NewType.Name;
        if (OriginalNameWasObfuscated)
            NewType.CustomAttributes.Add(new CustomAttribute(
                (ICustomAttributeType)assemblyContext.Imports.ObfuscatedNameAttributector.Value,
                new CustomAttributeSignature(new CustomAttributeArgument(assemblyContext.Imports.Module.String(), OriginalType.FullName))));

        if (!OriginalType.IsValueType)
            ComputedTypeSpecifics = TypeSpecifics.ReferenceType;
        else if (OriginalType.IsEnum)
            ComputedTypeSpecifics = TypeSpecifics.BlittableStruct;
        else if (OriginalType.HasGenericParameters())
            ComputedTypeSpecifics = TypeSpecifics.NonBlittableStruct; // not reference type, covered by first if
    }

    // These are initialized in AddMembers, which is called from an early rewrite pass.
    public IFieldDescriptor ClassPointerFieldRef { get; private set; } = null!;
    public ITypeDefOrRef SelfSubstitutedRef { get; private set; } = null!;

    public IEnumerable<FieldRewriteContext> Fields => myFieldContexts.Values;
    public IEnumerable<MethodRewriteContext> Methods => myMethodContexts.Values;

    public void AddMembers()
    {
        if (NewType.HasGenericParameters())
        {
            var genericInstanceType = new GenericInstanceTypeSignature(NewType, NewType.IsValueType);
            foreach (var newTypeGenericParameter in NewType.GenericParameters)
                genericInstanceType.TypeArguments.Add(newTypeGenericParameter.ToTypeSignature());
            SelfSubstitutedRef = NewType.Module!.DefaultImporter.ImportTypeSignature(genericInstanceType).ToTypeDefOrRef();
            var genericTypeRef = new GenericInstanceTypeSignature(
                AssemblyContext.Imports.Il2CppClassPointerStore.ToTypeDefOrRef(),
                AssemblyContext.Imports.Il2CppClassPointerStore.IsValueType,
                SelfSubstitutedRef.ToTypeSignature());
            ClassPointerFieldRef = ReferenceCreator.CreateFieldReference("NativeClassPtr", AssemblyContext.Imports.Module.IntPtr(),
                NewType.Module.DefaultImporter.ImportType(genericTypeRef.ToTypeDefOrRef()));
        }
        else
        {
            SelfSubstitutedRef = NewType;
            var genericTypeRef = new GenericInstanceTypeSignature(
                AssemblyContext.Imports.Il2CppClassPointerStore.ToTypeDefOrRef(),
                AssemblyContext.Imports.Il2CppClassPointerStore.IsValueType);
            if (OriginalType.ToTypeSignature().IsPrimitive() || OriginalType.FullName == "System.String")
                genericTypeRef.TypeArguments.Add(
                    NewType.Module!.ImportCorlibReference(OriginalType.FullName));
            else
                genericTypeRef.TypeArguments.Add(SelfSubstitutedRef.ToTypeSignature());
            ClassPointerFieldRef = ReferenceCreator.CreateFieldReference("NativeClassPtr", AssemblyContext.Imports.Module.IntPtr(),
                NewType.Module!.DefaultImporter.ImportType(genericTypeRef.ToTypeDefOrRef()));
        }

        if (OriginalType.IsEnum) return;

        var renamedFieldCounts = new Dictionary<string, int>();

        foreach (var originalTypeField in OriginalType.Fields)
            myFieldContexts[originalTypeField] = new FieldRewriteContext(this, originalTypeField, renamedFieldCounts);

        var hasExtensionMethods = false;

        foreach (var originalTypeMethod in OriginalType.Methods)
        {
            if (originalTypeMethod.IsStatic && originalTypeMethod.IsConstructor)
                continue;
            if (originalTypeMethod.IsConstructor
                && originalTypeMethod.Parameters is [{ ParameterType: CorLibTypeSignature { ElementType: ElementType.I } }])
                continue;
            var modules = this.AssemblyContext.GlobalContext.Assemblies.Select(a => a.OriginalAssembly.ManifestModule!);

            var methodRewriteContext = new MethodRewriteContext(this, originalTypeMethod);
            myMethodContexts[originalTypeMethod] = methodRewriteContext;
            myMethodContextsByName[originalTypeMethod.Name!] = methodRewriteContext;

            if (methodRewriteContext.HasExtensionAttribute) hasExtensionMethods = true;
        }

        if (hasExtensionMethods)
            NewType.CustomAttributes.Add(new CustomAttribute(AssemblyContext.Imports.Module.ExtensionAttributeCtor()));
    }

    public FieldRewriteContext GetFieldByOldField(FieldDefinition field)
    {
        return myFieldContexts[field];
    }

    public MethodRewriteContext GetMethodByOldMethod(MethodDefinition method)
    {
        return myMethodContexts[method];
    }

    public MethodRewriteContext? TryGetMethodByOldMethod(MethodDefinition method)
    {
        return myMethodContexts.TryGetValue(method, out var result) ? result : null;
    }

    public MethodRewriteContext? TryGetMethodByName(string name)
    {
        return myMethodContextsByName.TryGetValue(name, out var result) ? result : null;
    }

    public MethodRewriteContext? TryGetMethodByUnityAssemblyMethod(MethodDefinition method)
    {
        foreach (var methodRewriteContext in myMethodContexts)
        {
            var originalMethod = methodRewriteContext.Value.OriginalMethod;
            if (originalMethod.Name != method.Name) continue;
            if (originalMethod.Parameters.Count != method.Parameters.Count) continue;
            var badMethod = false;
            for (var i = 0; i < originalMethod.Parameters.Count; i++)
                if (originalMethod.Parameters[i].ParameterType.FullName != method.Parameters[i].ParameterType.FullName)
                {
                    badMethod = true;
                    break;
                }

            if (badMethod) continue;

            return methodRewriteContext.Value;
        }

        return null;
    }

    public FieldRewriteContext? TryGetFieldByUnityAssemblyField(FieldDefinition field)
    {
        foreach (var fieldRewriteContext in myFieldContexts)
        {
            var originalField = fieldRewriteContext.Value.OriginalField;
            if (originalField.Name != field.Name) continue;

            if (originalField.Signature?.FieldType.FullName != field.Signature?.FieldType.FullName)
                continue;

            return fieldRewriteContext.Value;
        }

        return null;
    }

    private string GetDebuggerDisplay()
    {
        return NewType.FullName;
    }
}
