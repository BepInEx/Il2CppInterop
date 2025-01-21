using AsmResolver.DotNet;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass12ComputeTypeSpecifics
{
    public static void DoPass(RewriteGlobalContext context)
    {
        typeUsageDictionary.Clear();

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ScanTypeContextUsage(typeContext);

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ComputeSpecifics(typeContext);

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ComputeSpecificsPass2(typeContext);
    }

    internal static Dictionary<TypeDefinition, ParameterUsage> typeUsageDictionary = new Dictionary<TypeDefinition, ParameterUsage>(new TypeComparer());

    private static void ScanTypeContextUsage(TypeRewriteContext typeContext)
    {
        var type = typeContext.OriginalType;

        foreach (FieldDefinition fieldDefinition in type.Fields)
        {
            ScanTypeUsage(fieldDefinition.Signature!.FieldType, type.GetGenericParameterContext());
        }

        foreach (PropertyDefinition propertyDefinition in type.Properties)
        {
            ScanTypeUsage(propertyDefinition.Signature!.ReturnType, type.GetGenericParameterContext());
        }

        foreach (MethodDefinition method in type.Methods)
        {
            ScanTypeUsage(method.Signature!.ReturnType, method.GetGenericParameterContext());
            foreach (Parameter parameterDefinition in method.Parameters)
            {
                ScanTypeUsage(parameterDefinition.ParameterType, method.GetGenericParameterContext());
            }
        }
    }

    private static void ScanTypeUsage(TypeSignature? fieldType, GenericParameterContext parameterContext)
    {
        while (fieldType is PointerTypeSignature pointerType)
        {
            fieldType = pointerType.BaseType;
        }

        while (fieldType is ArrayTypeSignature arrayType)
        {
            fieldType = arrayType.BaseType;
        }

        if (fieldType is GenericInstanceTypeSignature genericInstanceType)
        {
            foreach (TypeSignature typeReference in genericInstanceType.TypeArguments)
            {
                ScanTypeUsage(typeReference, parameterContext);
            }

            TypeDefinition typeDef = fieldType.Resolve()!;
            if (typeDef?.BaseType == null || !typeDef.BaseType.Name!.Equals("ValueType")) return;


            if (!typeUsageDictionary.TryGetValue(typeDef, out ParameterUsage usage))
            {
                usage = new ParameterUsage(genericInstanceType.TypeArguments.Count);
                typeUsageDictionary.Add(typeDef, usage);
            }

            for (var i = 0; i < genericInstanceType.TypeArguments.Count; i++)
            {
                usage.AddUsage(i, genericInstanceType.TypeArguments[i], parameterContext);
            }
        }
    }

    private static bool IsValueTypeOnly(TypeRewriteContext typeContext, GenericParameter genericParameter)
    {
        if (genericParameter.Constraints.All(constraint => constraint.Constraint!.FullName != "System.ValueType"))
            return false;

        if (typeUsageDictionary.ContainsKey(typeContext.OriginalType))
        {
            var usage = typeUsageDictionary[typeContext.OriginalType];
            return usage.IsBlittableParameter(typeContext.AssemblyContext.GlobalContext, genericParameter.Number);
        }

        return true;
    }

    private static void ComputeSpecifics(TypeRewriteContext typeContext)
    {
        if (typeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.NotComputed) return;
        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.Computing;

        foreach (var originalField in typeContext.OriginalType.Fields)
        {
            // Sometimes il2cpp metadata has invalid field offsets for some reason (https://github.com/SamboyCoding/Cpp2IL/issues/167)
            if (originalField.ExtractFieldOffset() >= 0x8000000)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            if (originalField.IsStatic) continue;

            var fieldType = originalField.Signature!.FieldType;

            if (fieldType.IsPrimitive() || fieldType is PointerTypeSignature or GenericParameterSignature) continue;

            if (fieldType.FullName == "System.String" || fieldType.FullName == "System.Object"
                || fieldType is ArrayBaseTypeSignature or ByReferenceTypeSignature)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            var fieldTypeContext = typeContext.AssemblyContext.GlobalContext.GetNewTypeForOriginal(fieldType.Resolve()!);
            ComputeSpecifics(fieldTypeContext);
            if (fieldTypeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
            {
                var genericInstance = fieldType as GenericInstanceTypeSignature;
                for (var i = 0; i < genericInstance!.TypeArguments.Count; i++)
                {
                    var genericArgument = genericInstance.TypeArguments[i];
                    if (genericArgument is GenericParameterSignature) continue;

                    if (fieldTypeContext.genericParameterUsage[i] < TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability) continue;

                    var genericArgumentContext = typeContext.AssemblyContext.GlobalContext.GetNewTypeForOriginal(genericArgument.Resolve());
                    ComputeSpecifics(genericArgumentContext);
                    if (genericArgumentContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.NonBlittableStruct ||
                        genericArgumentContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.ReferenceType)
                    {
                        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                        return;
                    }
                }
            }
            else if (fieldTypeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.BlittableStruct)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }
        }

        if (typeContext.OriginalType.GenericParameters.Count > 0)
        {
            typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.GenericBlittableStruct;
            return;
        }

        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.BlittableStruct;
    }

    private static void ComputeSpecificsPass2(TypeRewriteContext typeContext)
    {
        if (typeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.GenericBlittableStruct) return;

        foreach (var genericParameter in typeContext.OriginalType.GenericParameters)
        {
            if (typeContext.genericParameterUsage[genericParameter.Number] == TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability ||
                typeContext.genericParameterUsage[genericParameter.Number] == TypeRewriteContext.GenericParameterSpecifics.Strict)

            {
                if (IsValueTypeOnly(typeContext, genericParameter))
                {
                    typeContext.SetGenericParameterUsageSpecifics(genericParameter.Number, TypeRewriteContext.GenericParameterSpecifics.Strict);
                    continue;
                }

                return;
            }
        }

        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.BlittableStruct;
    }

    internal class ParameterUsage
    {
        public List<INameProvider>[] usageData;

        public ParameterUsage(int paramCount)
        {
            usageData = new List<INameProvider>[paramCount];
            for (var i = 0; i < paramCount; i++)
            {
                usageData[i] = new List<INameProvider>();
            }
        }

        public void AddUsage(int index, TypeSignature type, GenericParameterContext parameterContext)
        {
            if (type is GenericParameterSignature parameterSignature)
            {
                var genericParameter = parameterContext.GetGenericParameter(parameterSignature)!;
                var declaringName = GetDeclaringName(genericParameter);

                if (usageData[index].All(reference =>
                        reference is not GenericParameter parameter ||
                        !GetDeclaringName(parameter).Equals(declaringName))
                    )
                {
                    usageData[index].Add(genericParameter);
                }
            }
            else if (usageData[index].All(reference =>
                         reference is not TypeSignature fullNameProvider ||
                         !fullNameProvider.FullName.Equals(type.FullName)))
            {
                usageData[index].Add(type);
            }
        }

        private static string GetDeclaringName(GenericParameter genericParameter)
        {
            var declaringName = genericParameter.Owner!.FullName;
            declaringName += genericParameter.Name;
            return declaringName;
        }

        public bool IsBlittableParameter(RewriteGlobalContext globalContext, int index)
        {
            var usages = usageData[index];

            foreach (INameProvider reference in usages)
            {
                if (reference is GenericParameter genericParameter)
                {
                    if (genericParameter.Constraints.All(constraint => constraint.Constraint!.FullName != "System.ValueType"))
                        return false;
                }
                else if (reference is TypeSignature typeSignature)
                {
                    var typeDef = typeSignature.Resolve()!;
                    var fieldTypeContext = globalContext.GetNewTypeForOriginal(typeDef);
                    ComputeSpecifics(fieldTypeContext);
                    if (fieldTypeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.NonBlittableStruct)
                        return false;
                }
            }

            return true;
        }
    }

    private sealed class TypeComparer : EqualityComparer<TypeDefinition>
    {
        public override bool Equals(TypeDefinition x, TypeDefinition y)
        {
            return x.FullName.Equals(y.FullName);
        }

        public override int GetHashCode(TypeDefinition obj)
        {
            return obj.FullName.GetHashCode();
        }
    }
}
