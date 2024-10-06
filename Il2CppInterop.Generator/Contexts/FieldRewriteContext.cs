using System.Diagnostics;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Contexts;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class FieldRewriteContext
{
    private static readonly string[] MethodAccessTypeLabels =
        {"CompilerControlled", "Private", "FamAndAssem", "Internal", "Protected", "FamOrAssem", "Public"};

    public readonly TypeRewriteContext DeclaringType;
    public readonly FieldDefinition OriginalField;

    public readonly MemberReference PointerField;
    public readonly string UnmangledName;

    public FieldRewriteContext(TypeRewriteContext declaringType, FieldDefinition originalField,
        Dictionary<string, int>? renamedFieldCounts = null)
    {
        DeclaringType = declaringType;
        OriginalField = originalField;

        UnmangledName = UnmangleFieldName(originalField, declaringType.AssemblyContext.GlobalContext.Options,
            renamedFieldCounts);
        var pointerField = new FieldDefinition("NativeFieldInfoPtr_" + UnmangledName,
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly,
            declaringType.AssemblyContext.Imports.Module.IntPtr());

        declaringType.NewType.Fields.Add(pointerField);

        Debug.Assert(pointerField.Signature is not null);
        PointerField = new MemberReference(DeclaringType.SelfSubstitutedRef, pointerField.Name, new FieldSignature(pointerField.Signature!.FieldType));
    }

    private string UnmangleFieldNameBase(FieldDefinition field, GeneratorOptions options)
    {
        if (options.PassthroughNames)
            return field.Name!;

        if (!field.Name.IsObfuscated(options))
        {
            return field.Name.MakeValidInSource();
        }

        Debug.Assert(field.Signature is not null);
        var accessModString = MethodAccessTypeLabels[(int)(field.Attributes & FieldAttributes.FieldAccessMask)];
        var staticString = field.IsStatic ? "_Static" : "";
        return "field_" + accessModString + staticString + "_" +
               DeclaringType.AssemblyContext.RewriteTypeRef(field.Signature!.FieldType).GetUnmangledName(field.DeclaringType);
    }

    private string UnmangleFieldName(FieldDefinition field, GeneratorOptions options,
        Dictionary<string, int>? renamedFieldCounts)
    {
        if (options.PassthroughNames)
            return field.Name!;

        if (!field.Name.IsObfuscated(options))
        {
            return field.Name.MakeValidInSource();
        }

        if (renamedFieldCounts == null) throw new ArgumentNullException(nameof(renamedFieldCounts));

        var unmangleFieldNameBase = UnmangleFieldNameBase(field, options);

        renamedFieldCounts.TryGetValue(unmangleFieldNameBase, out var count);
        renamedFieldCounts[unmangleFieldNameBase] = count + 1;

        unmangleFieldNameBase += "_" + count;

        if (DeclaringType.AssemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                DeclaringType.NewType.GetNamespacePrefix() + "." + DeclaringType.NewType.Name + "::" +
                unmangleFieldNameBase, out var newName))
            unmangleFieldNameBase = newName;

        return unmangleFieldNameBase;
    }

    private string GetDebuggerDisplay()
    {
        return DeclaringType.NewType.FullName + "::" + UnmangledName;
    }
}
