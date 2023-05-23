using Il2CppInterop.Generator.Contexts;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Il2CppInterop.Generator.Extensions
{
    public static class GenericParameterEx
    {
        public static void MakeUnmanaged(this GenericParameter genericParameter, AssemblyRewriteContext assemblyContext)
        {
            var isUnmanagedAttribute = assemblyContext.GetOrInjectIsUnmanagedAttribute();
            genericParameter.HasDefaultConstructorConstraint = true;
            genericParameter.Constraints.Add(
                new GenericParameterConstraint(assemblyContext.Imports.Module.ImportReference(typeof(ValueType))
                    .MakeRequiredModifierType(
                        assemblyContext.Imports.Module.ImportReference(typeof(System.Runtime.InteropServices.UnmanagedType)))));

            genericParameter.CustomAttributes.Add(new CustomAttribute(isUnmanagedAttribute.Methods[0]));
            genericParameter.HasNotNullableValueTypeConstraint = true;
        }

        public static bool IsUnmanaged(this GenericParameter genericParameter)
        {
            return genericParameter.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Equals("IsUnmanagedAttribute"));
        }

    }
}
