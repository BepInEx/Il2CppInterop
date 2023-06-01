using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Passes
{
    public static class Pass11ComputeGenericParameterSpecifics
    {
        private static RewriteGlobalContext globalContext;

        public static void DoPass(RewriteGlobalContext context)
        {
            globalContext = context;

            foreach (var assemblyContext in context.Assemblies)
                foreach (var typeContext in assemblyContext.Types)
                    ComputeGenericParameterUsageSpecifics(typeContext);
        }

        private static void ComputeGenericParameterUsageSpecifics(TypeRewriteContext typeContext)
        {
            if (typeContext.genericParameterUsageComputed) return;
            typeContext.genericParameterUsageComputed = true;

            var originalType = typeContext.OriginalType;
            if (originalType.GenericParameters.Count == 0) return;

            void OnResult(GenericParameter parameter, TypeRewriteContext.GenericParameterSpecifics specific)
            {
                if (parameter.DeclaringType != originalType) return;

                typeContext.SetGenericParameterUsageSpecifics(parameter.Position, specific);
            }

            foreach (var originalField in originalType.Fields)
            {
                // Sometimes il2cpp metadata has invalid field offsets for some reason (https://github.com/SamboyCoding/Cpp2IL/issues/167)
                if (originalField.ExtractFieldOffset() >= 0x8000000) continue;
                if (originalField.IsStatic) continue;

                FindTypeGenericParameters(originalField.FieldType,
                    TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability, OnResult);
            }

            foreach (var originalField in originalType.Fields)
            {
                // Sometimes il2cpp metadata has invalid field offsets for some reason (https://github.com/SamboyCoding/Cpp2IL/issues/167)
                if (originalField.ExtractFieldOffset() >= 0x8000000) continue;
                if (!originalField.IsStatic) continue;

                FindTypeGenericParameters(originalField.FieldType,
                    TypeRewriteContext.GenericParameterSpecifics.Relaxed, OnResult);
            }

            foreach (var originalMethod in originalType.Methods)
            {
                FindTypeGenericParameters(originalMethod.ReturnType,
                    TypeRewriteContext.GenericParameterSpecifics.Relaxed, OnResult);

                foreach (ParameterDefinition parameter in originalMethod.Parameters)
                {
                    FindTypeGenericParameters(parameter.ParameterType,
                        TypeRewriteContext.GenericParameterSpecifics.Relaxed, OnResult);
                }
            }

            foreach (TypeDefinition nestedType in originalType.NestedTypes)
            {
                var nestedContext = globalContext.GetNewTypeForOriginal(nestedType);
                ComputeGenericParameterUsageSpecifics(nestedContext);

                foreach (var parameter in nestedType.GenericParameters)
                {
                    var myParameter = originalType.GenericParameters
                        .FirstOrDefault(param => param.Name.Equals(parameter.Name));

                    if (myParameter == null) continue;

                    var otherParameterSpecific = nestedContext.genericParameterUsage[parameter.Position];
                    if (otherParameterSpecific == TypeRewriteContext.GenericParameterSpecifics.Strict)
                        typeContext.SetGenericParameterUsageSpecifics(myParameter.Position, otherParameterSpecific);
                }
            }
        }

        private static void FindTypeGenericParameters(TypeReference reference,
            TypeRewriteContext.GenericParameterSpecifics currentConstraint,
            Action<GenericParameter, TypeRewriteContext.GenericParameterSpecifics> onFound)
        {
            if (reference is GenericParameter genericParameter)
            {
                onFound?.Invoke(genericParameter, currentConstraint);
                return;
            }

            if (reference.IsPointer)
            {
                FindTypeGenericParameters(reference.GetElementType(),
                    TypeRewriteContext.GenericParameterSpecifics.Strict, onFound);
                return;
            }

            if (reference.IsArray || reference.IsByReference)
            {
                FindTypeGenericParameters(reference.GetElementType(),
                    currentConstraint, onFound);
                return;
            }

            if (reference is GenericInstanceType genericInstance)
            {
                var typeContext = globalContext.GetNewTypeForOriginal(reference.Resolve());
                ComputeGenericParameterUsageSpecifics(typeContext);
                for (var i = 0; i < genericInstance.GenericArguments.Count; i++)
                {
                    var myConstraint = typeContext.genericParameterUsage[i];
                    if (myConstraint == TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability)
                        myConstraint = currentConstraint;

                    var genericArgument = genericInstance.GenericArguments[i];
                    FindTypeGenericParameters(genericArgument, myConstraint, onFound);
                }
            }
        }
    }
}
