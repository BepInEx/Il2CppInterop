using System.Diagnostics;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Microsoft.Extensions.Logging;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass12ComputeTypeSpecifics
{
    public static void DoPass(RewriteGlobalContext context)
    {
        typeUsageDictionary.Clear();

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ScanTypeUsage(typeContext);

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ComputeSpecifics(typeContext);

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ComputeSpecificsPass2(typeContext);
    }

    internal static Dictionary<TypeDefinition, ParameterUsage> typeUsageDictionary = new Dictionary<TypeDefinition, ParameterUsage>(new TypeComparer());

    private static void ScanTypeUsage(TypeRewriteContext typeContext)
    {
        foreach (FieldDefinition fieldDefinition in typeContext.OriginalType.Fields)
        {
            ScanTypeUsage(fieldDefinition.FieldType);
        }

        foreach (PropertyDefinition propertyDefinition in typeContext.OriginalType.Properties)
        {
            ScanTypeUsage(propertyDefinition.PropertyType);
        }

        foreach (MethodDefinition methodDefinition in typeContext.OriginalType.Methods)
        {
            ScanTypeUsage(methodDefinition.ReturnType);
            foreach (ParameterDefinition parameterDefinition in methodDefinition.Parameters)
            {
                ScanTypeUsage(parameterDefinition.ParameterType);
            }
        }
    }

    private static void ScanTypeUsage(TypeReference fieldType)
    {
        while (fieldType.IsPointer)
        {
            fieldType = (fieldType as PointerType).ElementType;
        }

        while (fieldType.IsArray)
        {
            fieldType = (fieldType as ArrayType).ElementType;
        }

        if (fieldType is GenericInstanceType genericInstanceType)
        {
            foreach (TypeReference typeReference in genericInstanceType.GenericArguments)
            {
                ScanTypeUsage(typeReference);
            }

            TypeDefinition typeDef = fieldType.Resolve();
            if (typeDef?.BaseType == null || !typeDef.BaseType.Name.Equals("ValueType")) return;


            if (!typeUsageDictionary.TryGetValue(typeDef, out ParameterUsage usage))
            {
                usage = new ParameterUsage(genericInstanceType.GenericArguments.Count);
                typeUsageDictionary.Add(typeDef, usage);
            }

            for (var i = 0; i < genericInstanceType.GenericArguments.Count; i++)
            {
                usage.AddUsage(i, genericInstanceType.GenericArguments[i]);
            }
        }
    }

    private static bool IsValueTypeOnly(TypeRewriteContext typeContext, GenericParameter genericParameter)
    {
        if (genericParameter.Constraints.All(constraint => constraint.ConstraintType.FullName != "System.ValueType"))
            return false;

        if (typeUsageDictionary.ContainsKey(typeContext.OriginalType))
        {
            var usage = typeUsageDictionary[typeContext.OriginalType];
            return usage.IsBlittableParameter(typeContext.AssemblyContext.GlobalContext, genericParameter.Position);
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

            var fieldType = originalField.FieldType;

            if (fieldType.IsPrimitive || fieldType.IsPointer || fieldType.IsGenericParameter) continue;

            if (fieldType.FullName == "System.String" || fieldType.FullName == "System.Object" || fieldType.IsArray ||
                fieldType.IsByReference)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            var fieldTypeContext = typeContext.AssemblyContext.GlobalContext.GetNewTypeForOriginal(fieldType.Resolve());
            ComputeSpecifics(fieldTypeContext);
            if (fieldTypeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
            {
                var genericInstance = fieldType as GenericInstanceType;
                for (var i = 0; i < genericInstance.GenericArguments.Count; i++)
                {
                    var genericArgument = genericInstance.GenericArguments[i];
                    if (genericArgument.IsGenericParameter) continue;
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
            if (typeContext.genericParameterUsage[genericParameter.Position] == TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability ||
                typeContext.genericParameterUsage[genericParameter.Position] == TypeRewriteContext.GenericParameterSpecifics.Strict)

            {
                if (IsValueTypeOnly(typeContext, genericParameter))
                {
                    typeContext.SetGenericParameterUsageSpecifics(genericParameter.Position, TypeRewriteContext.GenericParameterSpecifics.Strict);
                    continue;
                }

                return;
            }
        }

        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.BlittableStruct;
    }

    internal class ParameterUsage
    {
        public List<TypeReference>[] usageData;

        public ParameterUsage(int paramCount)
        {
            usageData = new List<TypeReference>[paramCount];
            for (var i = 0; i < paramCount; i++)
            {
                usageData[i] = new List<TypeReference>();
            }
        }

        public void AddUsage(int index, TypeReference type)
        {
            if (type is GenericParameter genericParameter)
            {
                var declaringName = GetDeclaringName(genericParameter);

                if (usageData[index].All(reference => reference is not GenericParameter parameter || !GetDeclaringName(parameter).Equals(declaringName)))
                {
                    usageData[index].Add(type);
                }
            }
            else if (usageData[index].All(reference => !reference.FullName.Equals(type.FullName)))
            {
                usageData[index].Add(type);
            }
        }

        private static string GetDeclaringName(GenericParameter genericParameter)
        {
            var declaringName = genericParameter.DeclaringMethod != null ? genericParameter.DeclaringMethod.FullName : genericParameter.DeclaringType.FullName;
            declaringName += genericParameter.FullName;
            return declaringName;
        }

        public bool IsBlittableParameter(RewriteGlobalContext globalContext, int index)
        {
            var usages = usageData[index];

            foreach (TypeReference reference in usages)
            {
                if (reference is GenericParameter genericParameter)
                {
                    if (genericParameter.Constraints.All(constraint => constraint.ConstraintType.FullName != "System.ValueType"))
                        return false;
                }
                else
                {
                    var typeDef = reference.Resolve();
                    var fieldTypeContext = globalContext.GetNewTypeForOriginal(typeDef);
                    ComputeSpecifics(fieldTypeContext);
                    if (fieldTypeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.NonBlittableStruct)
                        return false;
                }
            }

            return true;
        }
    }

    internal sealed class TypeComparer : EqualityComparer<TypeDefinition>
    {
        public override bool Equals(TypeDefinition x, TypeDefinition y)
        {
            if (x == null)
                return y == null;
            if (y == null)
                return false;

            return x.FullName.Equals(y.FullName);
        }

        public override int GetHashCode(TypeDefinition obj)
        {
            return obj.FullName.GetHashCode();
        }
    }
}
