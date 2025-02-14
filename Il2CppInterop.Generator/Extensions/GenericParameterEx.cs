using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Extensions
{
    public static class GenericParameterEx
    {
        public static void MakeUnmanaged(this GenericParameter genericParameter, AssemblyRewriteContext assemblyContext)
        {
            var isUnmanagedAttribute = assemblyContext.GetOrInjectIsUnmanagedAttribute();

            genericParameter.Attributes |= GenericParameterAttributes.DefaultConstructorConstraint | GenericParameterAttributes.NotNullableValueTypeConstraint;

            var importer = assemblyContext.Imports.Module.DefaultImporter;
            genericParameter.Constraints.Add(
                new GenericParameterConstraint(
                    importer.ImportType(typeof(ValueType))
                        .MakeModifierType(importer.ImportType(typeof(System.Runtime.InteropServices.UnmanagedType)), true)
                        .ToTypeDefOrRef()
                    ));

            genericParameter.CustomAttributes.Add(new CustomAttribute(isUnmanagedAttribute.Methods[0]));
        }

        public static bool IsUnmanaged(this GenericParameter genericParameter)
        {
            return genericParameter.CustomAttributes.Any(attribute => attribute.AttributeType()!.Name!.Equals("IsUnmanagedAttribute"));
        }
    }
}
