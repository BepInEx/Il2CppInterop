using System.Runtime.CompilerServices;
using System.Text;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Passes;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Contexts;

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
                                 (OriginalMethod?.Name?.IsObfuscated(declaringType.AssemblyContext.GlobalContext
                                     .Options) ?? false);

        var newMethod = new MethodDefinition("",
            AdjustAttributes(originalMethod.Attributes, originalMethod.Name == "Finalize"),
            declaringType.AssemblyContext.Imports.Module.Void());
        NewMethod = newMethod;

        HasExtensionAttribute =
            originalMethod.CustomAttributes.Any(x => x.AttributeType.FullName == typeof(ExtensionAttribute).FullName);

        if (HasExtensionAttribute)
            newMethod.CustomAttributes.Add(
                new CustomAttribute(declaringType.AssemblyContext.Imports.Module.ExtensionAttributeCtor()));

        if (originalMethod.HasGenericParameters)
        {
            var genericParams = originalMethod.GenericParameters;

            foreach (var oldParameter in genericParams)
            {
                var genericParameter = new GenericParameter(oldParameter.Name, newMethod);
                if (ShouldParameterBeBlittable(originalMethod, oldParameter))
                {
                    genericParameter.Attributes = oldParameter.Attributes;
                    genericParameter.MakeUnmanaged(DeclaringType.AssemblyContext);
                }
                else
                {
                    genericParameter.Attributes = oldParameter.Attributes.StripValueTypeConstraint();
                }

                newMethod.GenericParameters.Add(genericParameter);
            }
        }

        if (!Pass16GenerateMemberContexts.HasObfuscatedMethods && !passthroughNames &&
            originalMethod.Name.IsObfuscated(declaringType.AssemblyContext.GlobalContext.Options))
            Pass16GenerateMemberContexts.HasObfuscatedMethods = true;

        FileOffset = originalMethod.ExtractOffset();
        // Workaround for garbage file offsets passed by Cpp2IL
        if (FileOffset < 0) FileOffset = 0;
        Rva = originalMethod.ExtractRva();
        if (FileOffset != 0)
            declaringType.AssemblyContext.GlobalContext.MethodStartAddresses.Add(FileOffset);
    }

    private bool ShouldParameterBeBlittable(MethodDefinition method, GenericParameter genericParameter)
    {
        if (HasGenericParameter(method.ReturnType, genericParameter, out GenericParameter parameter))
        {
            return parameter.IsUnmanaged();
        }

        foreach (ParameterDefinition methodParameter in method.Parameters)
        {
            if (HasGenericParameter(methodParameter.ParameterType, genericParameter, out parameter))
            {
                return parameter.IsUnmanaged();
            }
        }

        return false;
    }

    private bool HasGenericParameter(TypeReference typeReference, GenericParameter inputGenericParameter, out GenericParameter typeGenericParameter)
    {
        typeGenericParameter = null;
        if (typeReference is not GenericInstanceType genericInstance) return false;

        var index = genericInstance.GenericArguments.IndexOf(inputGenericParameter);
        if (index < 0) return false;

        var globalContext = DeclaringType.AssemblyContext.GlobalContext;
        var returnTypeContext = globalContext.GetNewTypeForOriginal(typeReference.Resolve());
        typeGenericParameter = returnTypeContext.NewType.GenericParameters[index];
        return true;
    }

    public string UnmangledName { get; private set; }
    public string UnmangledNameWithSignature { get; private set; }

    public TypeDefinition? GenericInstantiationsStore { get; private set; }
    public TypeReference? GenericInstantiationsStoreSelfSubstRef { get; private set; }
    public TypeReference? GenericInstantiationsStoreSelfSubstMethodRef { get; private set; }
    public FieldReference NonGenericMethodInfoPointerField { get; private set; }

    public bool HasExtensionAttribute { get; }

    public void CtorPhase2()
    {
        UnmangledName = UnmangleMethodName();
        UnmangledNameWithSignature = UnmangleMethodNameWithSignature();

        NewMethod.Name = UnmangledName;
        NewMethod.ReturnType = DeclaringType.AssemblyContext.RewriteTypeRef(OriginalMethod.ReturnType, DeclaringType.isBoxedTypeVariant);

        var nonGenericMethodInfoPointerField = new FieldDefinition(
            "NativeMethodInfoPtr_" + UnmangledNameWithSignature,
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly,
            DeclaringType.AssemblyContext.Imports.Module.IntPtr());
        DeclaringType.NewType.Fields.Add(nonGenericMethodInfoPointerField);

        NonGenericMethodInfoPointerField = new FieldReference(nonGenericMethodInfoPointerField.Name,
            nonGenericMethodInfoPointerField.FieldType, DeclaringType.SelfSubstitutedRef);

        if (OriginalMethod.HasGenericParameters)
        {
            var genericParams = OriginalMethod.GenericParameters;
            var genericMethodInfoStoreType = new TypeDefinition("",
                "MethodInfoStoreGeneric_" + UnmangledNameWithSignature + "`" + genericParams.Count,
                TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                DeclaringType.AssemblyContext.Imports.Module.Object());
            genericMethodInfoStoreType.DeclaringType = DeclaringType.NewType;
            DeclaringType.NewType.NestedTypes.Add(genericMethodInfoStoreType);
            GenericInstantiationsStore = genericMethodInfoStoreType;

            var selfSubstRef = new GenericInstanceType(genericMethodInfoStoreType);
            var selfSubstMethodRef = new GenericInstanceType(genericMethodInfoStoreType);

            for (var index = 0; index < genericParams.Count; index++)
            {
                var oldParameter = genericParams[index];
                var genericParameter = new GenericParameter(oldParameter.Name, genericMethodInfoStoreType);
                genericMethodInfoStoreType.GenericParameters.Add(genericParameter);
                selfSubstRef.GenericArguments.Add(genericParameter);
                var newParameter = NewMethod.GenericParameters[index];
                selfSubstMethodRef.GenericArguments.Add(newParameter);

                foreach (var oldConstraint in oldParameter.Constraints)
                {
                    if (oldConstraint.ConstraintType.FullName == "System.ValueType" ||
                        oldConstraint.ConstraintType.Resolve()?.IsInterface == true) continue;

                    newParameter.Constraints.Add(new GenericParameterConstraint(
                        DeclaringType.AssemblyContext.RewriteTypeRef(oldConstraint.ConstraintType, DeclaringType.isBoxedTypeVariant)));
                }
            }

            var pointerField = new FieldDefinition("Pointer", FieldAttributes.Assembly | FieldAttributes.Static,
                DeclaringType.AssemblyContext.Imports.Module.IntPtr());
            genericMethodInfoStoreType.Fields.Add(pointerField);

            GenericInstantiationsStoreSelfSubstRef = DeclaringType.NewType.Module.ImportReference(selfSubstRef);
            GenericInstantiationsStoreSelfSubstMethodRef =
                DeclaringType.NewType.Module.ImportReference(selfSubstMethodRef);
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
            return method.Name;

        if (method.Name == ".ctor")
            return ".ctor";

        if (method.Name.IsObfuscated(DeclaringType.AssemblyContext.GlobalContext.Options))
            return UnmangleMethodNameWithSignature();

        if (method.Name.IsInvalidInSource())
            return method.Name.FilterInvalidInSourceChars();

        return method.Name;
    }

    private string ProduceMethodSignatureBase()
    {
        var method = OriginalMethod;

        var name = method.Name;
        if (method.Name.IsObfuscated(DeclaringType.AssemblyContext.GlobalContext.Options))
            name = "Method";

        if (name.IsInvalidInSource())
            name = name.FilterInvalidInSourceChars();

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
        foreach (var (semantic, str) in SemanticsToCheck)
            if ((semantic & method.SemanticsAttributes) != 0)
                builder.Append(str);

        builder.Append('_');
        builder.Append(DeclaringType.AssemblyContext.RewriteTypeRef(method.ReturnType, DeclaringType.isBoxedTypeVariant).GetUnmangledName());

        foreach (var param in method.Parameters)
        {
            builder.Append('_');
            builder.Append(DeclaringType.AssemblyContext.RewriteTypeRef(param.ParameterType, DeclaringType.isBoxedTypeVariant).GetUnmangledName());
        }

        var address = Rva;
        if (address != 0 && Pass16GenerateMemberContexts.HasObfuscatedMethods &&
            !Pass17ScanMethodRefs.NonDeadMethods.Contains(address)) builder.Append("_PDM");

        return builder.ToString();
    }


    private string UnmangleMethodNameWithSignature()
    {
        var unmangleMethodNameWithSignature = ProduceMethodSignatureBase() + "_" + DeclaringType.Methods
            .Where(ParameterSignatureMatchesThis).TakeWhile(it => it != this).Count();
        if (DeclaringType.AssemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                DeclaringType.NewType.GetNamespacePrefix() + "::" + unmangleMethodNameWithSignature, out var newName))
            unmangleMethodNameWithSignature = newName;
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

        if (aM.SemanticsAttributes != bM.SemanticsAttributes)
            return false;

        if (aM.ReturnType.FullName != bM.ReturnType.FullName)
            return false;

        var a = aM.Parameters;
        var b = bM.Parameters;

        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
            if (a[i].ParameterType.FullName != b[i].ParameterType.FullName)
                return false;

        if (Pass16GenerateMemberContexts.HasObfuscatedMethods)
        {
            var addressA = otherRewriteContext.Rva;
            var addressB = Rva;
            if (addressA != 0 && addressB != 0)
                if (Pass17ScanMethodRefs.NonDeadMethods.Contains(addressA) !=
                    Pass17ScanMethodRefs.NonDeadMethods.Contains(addressB))
                    return false;
        }

        return true;
    }
}
