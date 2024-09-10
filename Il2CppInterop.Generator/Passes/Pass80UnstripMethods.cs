using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass80UnstripMethods
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var methodsUnstripped = 0;
        var methodsIgnored = 0;

        foreach (var unityAssembly in context.UnityAssemblies.Assemblies)
        {
            var processedAssembly = context.TryGetAssemblyByName(unityAssembly.Name);
            if (processedAssembly == null) continue;
            var imports = processedAssembly.Imports;

            foreach (var unityType in unityAssembly.ManifestModule!.TopLevelTypes)
            {
                var processedType = processedAssembly.TryGetTypeByName(unityType.FullName);
                if (processedType == null) continue;

                foreach (var unityMethod in unityType.Methods)
                {
                    var isICall = (unityMethod.ImplAttributes & MethodImplAttributes.InternalCall) != 0;
                    if (unityMethod.IsConstructor) continue;
                    if (unityMethod.IsAbstract) continue;
                    if (!unityMethod.HasMethodBody && !isICall) continue; // CoreCLR chokes on no-body methods

                    var processedMethod = processedType.TryGetMethodByUnityAssemblyMethod(unityMethod);
                    if (processedMethod != null) continue;

                    var returnType = ResolveTypeInNewAssemblies(context, unityMethod.Signature!.ReturnType, imports);
                    if (returnType == null)
                    {
                        Logger.Instance.LogTrace("Method {UnityMethod} has unsupported return type {UnityMethodReturnType}", unityMethod.ToString(), unityMethod.Signature.ReturnType.ToString());
                        methodsIgnored++;
                        continue;
                    }

                    var newAttributes = (unityMethod.Attributes & ~MethodAttributes.MemberAccessMask) | MethodAttributes.Public;
                    var newMethod = new MethodDefinition(unityMethod.Name,
                        newAttributes,
                        MethodSignatureCreator.CreateMethodSignature(newAttributes, returnType, unityMethod.Signature.GenericParameterCount));
                    newMethod.CilMethodBody = new(newMethod);
                    var hadBadParameter = false;
                    foreach (var unityMethodParameter in unityMethod.Parameters)
                    {
                        var convertedType =
                            ResolveTypeInNewAssemblies(context, unityMethodParameter.ParameterType, imports);
                        if (convertedType == null)
                        {
                            hadBadParameter = true;
                            Logger.Instance.LogTrace("Method {UnityMethod} has unsupported parameter type {UnityMethodParameter}", unityMethod.ToString(), unityMethodParameter.ToString());
                            break;
                        }

                        newMethod.AddParameter(convertedType, unityMethodParameter.Name, unityMethodParameter.Definition?.Attributes ?? default);
                    }

                    if (hadBadParameter)
                    {
                        methodsIgnored++;
                        continue;
                    }

                    foreach (var unityMethodGenericParameter in unityMethod.GenericParameters)
                    {
                        var newParameter = new GenericParameter(unityMethodGenericParameter.Name);
                        newParameter.Attributes = unityMethodGenericParameter.Attributes;
                        foreach (var genericParameterConstraint in unityMethodGenericParameter.Constraints)
                        {
                            if (genericParameterConstraint.IsSystemValueType() || genericParameterConstraint.IsInterface())
                                continue;

                            if (genericParameterConstraint.IsSystemEnum())
                            {
                                newParameter.Constraints.Add(new GenericParameterConstraint(imports.Module.Enum().ToTypeDefOrRef()));
                                continue;
                            }

                            var newType = ResolveTypeInNewAssemblies(context, genericParameterConstraint.Constraint?.ToTypeSignature(),
                                imports);
                            if (newType != null)
                                newParameter.Constraints.Add(new GenericParameterConstraint(newType.ToTypeDefOrRef()));
                        }

                        newMethod.GenericParameters.Add(newParameter);
                    }

                    if (isICall)
                    {
                        var delegateType =
                            UnstripGenerator.CreateDelegateTypeForICallMethod(unityMethod, newMethod, imports);
                        processedType.NewType.NestedTypes.Add(delegateType);

                        processedType.NewType.Methods.Add(newMethod);

                        var delegateField = UnstripGenerator.GenerateStaticCtorSuffix(processedType.NewType,
                            delegateType, unityMethod, imports);
                        UnstripGenerator.GenerateInvokerMethodBody(newMethod, delegateField, delegateType,
                            processedType, imports);
                    }
                    else
                    {
                        Pass81FillUnstrippedMethodBodies.PushMethod(unityMethod, newMethod, processedType, imports);
                        processedType.NewType.Methods.Add(newMethod);
                    }

                    if (unityMethod.IsGetMethod)
                    {
                        var property = GetOrCreateProperty(unityMethod, newMethod);
                        property.GetMethod = newMethod;
                    }
                    else if (unityMethod.IsSetMethod)
                    {
                        var property = GetOrCreateProperty(unityMethod, newMethod);
                        property.SetMethod = newMethod;
                    }

                    var paramsMethod = context.CreateParamsMethod(unityMethod, newMethod, imports,
                        type => ResolveTypeInNewAssemblies(context, type, imports));
                    if (paramsMethod != null) processedType.NewType.Methods.Add(paramsMethod);

                    methodsUnstripped++;
                }
            }
        }

        Logger.Instance.LogInformation("Restored {UnstrippedMethods} methods", methodsUnstripped);
        Logger.Instance.LogInformation("Failed to restore {IgnoredMethods} methods", methodsIgnored);
    }

    private static PropertyDefinition GetOrCreateProperty(MethodDefinition unityMethod, MethodDefinition newMethod)
    {
        var unityProperty =
            unityMethod.DeclaringType!.Properties.Single(
                it => it.SetMethod == unityMethod || it.GetMethod == unityMethod);
        var newProperty = newMethod.DeclaringType?.Properties.SingleOrDefault(it =>
            it.Name == unityProperty.Name && it.Signature!.ParameterTypes.Count == unityProperty.Signature!.ParameterTypes.Count &&
            it.Signature.ParameterTypes.SequenceEqual(unityProperty.Signature.ParameterTypes, SignatureComparer.Default));
        if (newProperty == null)
        {
            TypeSignature propertyType;
            IEnumerable<TypeSignature> parameterTypes;
            if (unityMethod.IsGetMethod)
            {
                propertyType = newMethod.Signature!.ReturnType;
                parameterTypes = newMethod.Signature.ParameterTypes;
            }
            else
            {
                propertyType = newMethod.Signature!.ParameterTypes.Last();
                parameterTypes = newMethod.Signature.ParameterTypes.Take(newMethod.Signature.ParameterTypes.Count - 1);
            }

            var propertySignature = unityProperty.Signature!.HasThis
                ? PropertySignature.CreateInstance(propertyType, parameterTypes)
                : PropertySignature.CreateStatic(propertyType, parameterTypes);
            newProperty = new PropertyDefinition(unityProperty.Name, unityProperty.Attributes, propertySignature);
            newMethod.DeclaringType!.Properties.Add(newProperty);
        }

        return newProperty;
    }

    internal static TypeSignature? ResolveTypeInNewAssemblies(RewriteGlobalContext context, TypeSignature? unityType,
        RuntimeAssemblyReferences imports)
    {
        var resolved = ResolveTypeInNewAssembliesRaw(context, unityType, imports);
        return resolved != null ? imports.Module.DefaultImporter.ImportTypeSignature(resolved) : null;
    }

    internal static TypeSignature? ResolveTypeInNewAssembliesRaw(RewriteGlobalContext context, TypeSignature? unityType,
        RuntimeAssemblyReferences imports)
    {
        if (unityType is null or GenericParameterSignature)
            return null;

        if (unityType is ByReferenceTypeSignature)
        {
            var resolvedElementType = ResolveTypeInNewAssemblies(context, unityType.GetElementType(), imports);
            return resolvedElementType == null ? null : new ByReferenceTypeSignature(resolvedElementType);
        }

        if (unityType is ArrayBaseTypeSignature arrayType)
        {
            if (arrayType.Rank != 1) return null;
            var resolvedElementType = ResolveTypeInNewAssemblies(context, unityType.GetElementType(), imports);
            if (resolvedElementType == null) return null;
            if (resolvedElementType.FullName == "System.String")
                return imports.Il2CppStringArray;
            var genericBase = resolvedElementType.IsValueType
                ? imports.Il2CppStructArray
                : imports.Il2CppReferenceArray;
            return new GenericInstanceTypeSignature(genericBase.ToTypeDefOrRef(), false, resolvedElementType);
        }

        if (unityType.DeclaringType != null)
        {
            var enclosingResolvedType = ResolveTypeInNewAssembliesRaw(context, unityType.DeclaringType.ToTypeSignature(), imports);
            if (enclosingResolvedType == null) return null;
            var resolvedNestedType = enclosingResolvedType.Resolve()!.NestedTypes
                .FirstOrDefault(it => it.Name == unityType.Name);

            return resolvedNestedType?.ToTypeSignature();
        }

        if (unityType is PointerTypeSignature)
        {
            var resolvedElementType = ResolveTypeInNewAssemblies(context, unityType.GetElementType(), imports);
            return resolvedElementType?.MakePointerType();
        }

        if (unityType is GenericInstanceTypeSignature genericInstance)
        {
            var baseRef = ResolveTypeInNewAssembliesRaw(context, genericInstance.GenericType.ToTypeSignature(), imports);
            if (baseRef == null) return null;
            var newInstance = new GenericInstanceTypeSignature(baseRef.ToTypeDefOrRef(), baseRef.IsValueType);
            foreach (var unityGenericArgument in genericInstance.TypeArguments)
            {
                var resolvedArgument = ResolveTypeInNewAssemblies(context, unityGenericArgument, imports);
                if (resolvedArgument == null) return null;
                newInstance.TypeArguments.Add(resolvedArgument);
            }

            return newInstance;
        }

        var targetAssemblyName = unityType.Scope!.Name!;
        if (targetAssemblyName.EndsWith(".dll"))
            targetAssemblyName = targetAssemblyName.Substring(0, targetAssemblyName.Length - 4);
        if ((targetAssemblyName == "mscorlib" || targetAssemblyName == "netstandard") &&
            (unityType.IsValueType || unityType.FullName == "System.String" ||
             unityType.FullName == "System.Void") && unityType.FullName != "System.RuntimeTypeHandle")
            return imports.Module.ImportCorlibReference(unityType.FullName);

        if (targetAssemblyName == "UnityEngine")
            foreach (var assemblyRewriteContext in context.Assemblies)
            {
                if (!assemblyRewriteContext.NewAssembly.Name.StartsWith("UnityEngine"))
                    continue;

                var newTypeInAnyUnityAssembly =
                    assemblyRewriteContext.TryGetTypeByName(unityType.FullName)?.NewType;
                if (newTypeInAnyUnityAssembly != null)
                    return newTypeInAnyUnityAssembly.ToTypeSignature();
            }

        var targetAssembly = context.TryGetAssemblyByName(targetAssemblyName);
        var newType = targetAssembly?.TryGetTypeByName(unityType.FullName)?.NewType.ToTypeSignature();

        return newType;
    }
}
