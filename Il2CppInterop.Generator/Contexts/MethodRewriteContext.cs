using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Passes;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Contexts;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class MethodRewriteContext
{
    private static readonly string[] MethodAccessTypeLabels =
        {"CompilerControlled", "Private", "FamAndAssem", "Internal", "Protected", "FamOrAssem", "Public"};

    private static readonly (MethodSemanticsAttributes, string)[] SemanticsToCheck =
    {
        (MethodSemanticsAttributes.Setter, "_set"),
        (MethodSemanticsAttributes.Getter, "_get"),
        (MethodSemanticsAttributes.Other, "_oth"),
        (MethodSemanticsAttributes.AddOn, "_add"),
        (MethodSemanticsAttributes.RemoveOn, "_rem"),
        (MethodSemanticsAttributes.Fire, "_fire")
    };

    public readonly TypeRewriteContext DeclaringType;

    public readonly long FileOffset;
    public readonly MethodDefinition NewMethod;
    public readonly MethodDefinition OriginalMethod;

    public readonly bool OriginalNameObfuscated;
    public readonly long Rva;

    public readonly List<XrefInstance> XrefScanResults = new();

    public long MetadataInitFlagRva;
    public long MetadataInitTokenRva;

    public MethodRewriteContext(TypeRewriteContext declaringType, MethodDefinition originalMethod)
    {
        DeclaringType = declaringType;
        OriginalMethod = originalMethod;

        var passthroughNames = declaringType.AssemblyContext.GlobalContext.Options.PassthroughNames;

        OriginalNameObfuscated = !passthroughNames &&
                                 (OriginalMethod.Name?.IsObfuscated(declaringType.AssemblyContext.GlobalContext
                                     .Options) ?? false);

        var newAttributes = AdjustAttributes(originalMethod.Attributes, originalMethod.Name == "Finalize");
        var newSignature = (newAttributes & MethodAttributes.Static) != 0
            ? MethodSignature.CreateStatic(declaringType.AssemblyContext.Imports.Module.Void(), originalMethod.GenericParameters.Count)
            : MethodSignature.CreateInstance(declaringType.AssemblyContext.Imports.Module.Void(), originalMethod.GenericParameters.Count);
        var newMethod = new MethodDefinition("", newAttributes, newSignature);
        newMethod.CilMethodBody = new(newMethod);
        NewMethod = newMethod;

        HasExtensionAttribute =
            originalMethod.CustomAttributes.Any(x => x.AttributeType()?.FullName == typeof(ExtensionAttribute).FullName);

        if (HasExtensionAttribute)
            newMethod.CustomAttributes.Add(
                new CustomAttribute(declaringType.AssemblyContext.Imports.Module.ExtensionAttributeCtor()));

        if (originalMethod.HasGenericParameters())
        {
            var genericParams = originalMethod.GenericParameters;

            foreach (var oldParameter in genericParams)
            {
                newMethod.GenericParameters.Add(new GenericParameter(
                    oldParameter.Name.MakeValidInSource(),
                    oldParameter.Attributes.StripValueTypeConstraint()));
            }
        }

        if (!Pass15GenerateMemberContexts.HasObfuscatedMethods && !passthroughNames &&
            originalMethod.Name.IsObfuscated(declaringType.AssemblyContext.GlobalContext.Options))
            Pass15GenerateMemberContexts.HasObfuscatedMethods = true;

        FileOffset = originalMethod.ExtractOffset();
        // Workaround for garbage file offsets passed by Cpp2IL
        if (FileOffset < 0) FileOffset = 0;
        Rva = originalMethod.ExtractRva();
        if (FileOffset != 0)
            declaringType.AssemblyContext.GlobalContext.MethodStartAddresses.Add(FileOffset);
    }

    public Utf8String? UnmangledName { get; private set; }
    public string? UnmangledNameWithSignature { get; private set; }

    public TypeDefinition? GenericInstantiationsStore { get; private set; }
    public ITypeDefOrRef? GenericInstantiationsStoreSelfSubstRef { get; private set; }
    public ITypeDefOrRef? GenericInstantiationsStoreSelfSubstMethodRef { get; private set; }
    public MemberReference NonGenericMethodInfoPointerField { get; private set; } = null!; // Initialized in CtorPhase2

    public bool HasExtensionAttribute { get; }

    public void CtorPhase2()
    {
        UnmangledName = UnmangleMethodName();
        UnmangledNameWithSignature = UnmangleMethodNameWithSignature();

        NewMethod.Name = UnmangledName;
        NewMethod.Signature!.ReturnType = DeclaringType.AssemblyContext.RewriteTypeRef(OriginalMethod.Signature?.ReturnType);

        var nonGenericMethodInfoPointerField = new FieldDefinition(
            "NativeMethodInfoPtr_" + UnmangledNameWithSignature,
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly,
            DeclaringType.AssemblyContext.Imports.Module.IntPtr());
        DeclaringType.NewType.Fields.Add(nonGenericMethodInfoPointerField);

        NonGenericMethodInfoPointerField = new MemberReference(DeclaringType.SelfSubstitutedRef, nonGenericMethodInfoPointerField.Name,
            new FieldSignature(nonGenericMethodInfoPointerField.Signature!.FieldType));

        if (OriginalMethod.HasGenericParameters())
        {
            var genericParams = OriginalMethod.GenericParameters;
            var genericMethodInfoStoreType = new TypeDefinition("",
                "MethodInfoStoreGeneric_" + UnmangledNameWithSignature + "`" + genericParams.Count,
                TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                DeclaringType.AssemblyContext.Imports.Module.Object().ToTypeDefOrRef());
            DeclaringType.NewType.NestedTypes.Add(genericMethodInfoStoreType);
            GenericInstantiationsStore = genericMethodInfoStoreType;

            var selfSubstRef = new GenericInstanceTypeSignature(genericMethodInfoStoreType, false);
            var selfSubstMethodRef = new GenericInstanceTypeSignature(genericMethodInfoStoreType, false);

            for (var index = 0; index < genericParams.Count; index++)
            {
                var oldParameter = genericParams[index];
                var genericParameter = new GenericParameter(oldParameter.Name.MakeValidInSource());
                genericMethodInfoStoreType.GenericParameters.Add(genericParameter);
                selfSubstRef.TypeArguments.Add(genericParameter.ToTypeSignature());
                var newParameter = NewMethod.GenericParameters[index];
                selfSubstMethodRef.TypeArguments.Add(newParameter.ToTypeSignature());

                foreach (var oldConstraint in oldParameter.Constraints)
                {
                    if (oldConstraint.IsSystemValueType() || oldConstraint.IsInterface())
                        continue;

                    if (oldConstraint.IsSystemEnum())
                    {
                        newParameter.Constraints.Add(new GenericParameterConstraint(
                            DeclaringType.AssemblyContext.Imports.Module.Enum().ToTypeDefOrRef()));
                        continue;
                    }

                    newParameter.Constraints.Add(new GenericParameterConstraint(
                        DeclaringType.AssemblyContext.RewriteTypeRef(oldConstraint.Constraint?.ToTypeSignature()).ToTypeDefOrRef()));
                }
            }

            var pointerField = new FieldDefinition("Pointer", FieldAttributes.Assembly | FieldAttributes.Static,
                DeclaringType.AssemblyContext.Imports.Module.IntPtr());
            genericMethodInfoStoreType.Fields.Add(pointerField);

            GenericInstantiationsStoreSelfSubstRef = DeclaringType.NewType.Module!.DefaultImporter.ImportType(selfSubstRef.ToTypeDefOrRef());
            GenericInstantiationsStoreSelfSubstMethodRef =
                DeclaringType.NewType.Module.DefaultImporter.ImportType(selfSubstMethodRef.ToTypeDefOrRef());
        }

        DeclaringType.NewType.Methods.Add(NewMethod);
    }

    private MethodAttributes AdjustAttributes(MethodAttributes original, bool stripVirtual)
    {
        original &= ~MethodAttributes.MemberAccessMask; // todo: handle Object overload correctly
        original &= ~MethodAttributes.PInvokeImpl;
        original &= ~MethodAttributes.Abstract;
        if (stripVirtual) original &= ~MethodAttributes.Virtual;
        original &= ~MethodAttributes.Final;
        if (stripVirtual) original &= ~MethodAttributes.NewSlot;
        original &= ~MethodAttributes.ReuseSlot;
        original &= ~MethodAttributes.CheckAccessOnOverride;
        original |= MethodAttributes.Public;
        return original;
    }

    private string UnmangleMethodName()
    {
        var method = OriginalMethod;

        if (method.Name == "GetType" && method.Parameters.Count == 0)
            return "GetIl2CppType";

        if (DeclaringType.AssemblyContext.GlobalContext.Options.PassthroughNames)
            return method.Name!;

        if (method.Name == ".ctor")
            return ".ctor";

        if (method.Name.IsObfuscated(DeclaringType.AssemblyContext.GlobalContext.Options))
            return UnmangleMethodNameWithSignature();

        return method.Name.MakeValidInSource();
    }

    private string ProduceMethodSignatureBase()
    {
        var method = OriginalMethod;

        string name;
        if (method.Name.IsObfuscated(DeclaringType.AssemblyContext.GlobalContext.Options))
            name = "Method";
        else
            name = method.Name.MakeValidInSource();

        if (method.Name == "GetType" && method.Parameters.Count == 0)
            name = "GetIl2CppType";

        var builder = new StringBuilder();
        builder.Append(name);
        builder.Append('_');
        builder.Append(MethodAccessTypeLabels[(int)(method.Attributes & MethodAttributes.MemberAccessMask)]);
        if (method.IsAbstract) builder.Append("_Abstract");
        if (method.IsVirtual) builder.Append("_Virtual");
        if (method.IsStatic) builder.Append("_Static");
        if (method.IsFinal) builder.Append("_Final");
        if (method.IsNewSlot) builder.Append("_New");
        if (method.Semantics is not null)
            foreach (var (semantic, str) in SemanticsToCheck)
                if ((semantic & method.Semantics.Attributes) != 0)
                    builder.Append(str);

        builder.Append('_');
        builder.Append(DeclaringType.AssemblyContext.RewriteTypeRef(method.Signature?.ReturnType).GetUnmangledName(method.DeclaringType, method));

        foreach (var param in method.Parameters)
        {
            builder.Append('_');
            builder.Append(DeclaringType.AssemblyContext.RewriteTypeRef(param.ParameterType).GetUnmangledName(method.DeclaringType, method));
        }

        var address = Rva;
        if (address != 0 && Pass15GenerateMemberContexts.HasObfuscatedMethods &&
            !Pass16ScanMethodRefs.NonDeadMethods.Contains(address)) builder.Append("_PDM");

        return builder.ToString();
    }


    private string UnmangleMethodNameWithSignature()
    {
        var unmangleMethodNameWithSignature = ProduceMethodSignatureBase() + "_" + DeclaringType.Methods
            .Where(ParameterSignatureMatchesThis).TakeWhile(it => it != this).Count();

        if (DeclaringType.AssemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                DeclaringType.NewType.GetNamespacePrefix() + "." + DeclaringType.NewType.Name + "::" + unmangleMethodNameWithSignature, out var newNameByType))
        {
            unmangleMethodNameWithSignature = newNameByType;
        }
        else if (DeclaringType.AssemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                DeclaringType.NewType.GetNamespacePrefix() + "::" + unmangleMethodNameWithSignature, out var newName))
        {
            unmangleMethodNameWithSignature = newName;
        }

        return unmangleMethodNameWithSignature;
    }

    private bool ParameterSignatureMatchesThis(MethodRewriteContext otherRewriteContext)
    {
        var aM = otherRewriteContext.OriginalMethod;
        var bM = OriginalMethod;

        if (!otherRewriteContext.OriginalNameObfuscated)
            return false;

        var comparisonMask = MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.Final |
                             MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot;
        if ((aM.Attributes & comparisonMask) !=
            (bM.Attributes & comparisonMask))
            return false;

        if (aM.Semantics?.Attributes != bM.Semantics?.Attributes)
            return false;

        if (aM.Signature?.ReturnType.FullName != bM.Signature?.ReturnType.FullName)
            return false;

        var a = aM.Parameters;
        var b = bM.Parameters;

        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
            if (a[i].ParameterType.FullName != b[i].ParameterType.FullName)
                return false;

        if (Pass15GenerateMemberContexts.HasObfuscatedMethods)
        {
            var addressA = otherRewriteContext.Rva;
            var addressB = Rva;
            if (addressA != 0 && addressB != 0)
                if (Pass16ScanMethodRefs.NonDeadMethods.Contains(addressA) !=
                    Pass16ScanMethodRefs.NonDeadMethods.Contains(addressB))
                    return false;
        }

        return true;
    }

    private string GetDebuggerDisplay()
    {
        return NewMethod.FullName;
    }
}
