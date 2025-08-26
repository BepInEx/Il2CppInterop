using System.Diagnostics.CodeAnalysis;
using AsmResolver.DotNet;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using LibCpp2IL.BinaryStructures;

namespace Il2CppInterop.Generator;

public abstract class UnstripBaseProcessingLayer : Cpp2IlProcessingLayer
{
    public const string AssembliesKey = "unstrip-assemblies";
    public const string DirectoryKey = "unstrip-directory";

    protected static void InjectAssemblies(ApplicationAnalysisContext appContext, IReadOnlyList<AssemblyDefinition> assemblyList, bool storeMethodBodies)
    {
        // Inject assemblies
        var assemblyDictionary = new Dictionary<ModuleDefinition, AssemblyAnalysisContext>(assemblyList.Count);
        foreach (var assembly in assemblyList)
        {
            if (IsEmpty(assembly.ManifestModule!))
                continue; // Skip empty assemblies

            if (!appContext.AssembliesByName.TryGetValue(assembly.Name!, out var assemblyContext))
            {
                assemblyContext = appContext.InjectAssembly(assembly.Name!, assembly.Version, (uint)assembly.HashAlgorithm, (uint)assembly.Attributes, assembly.Culture, null, assembly.PublicKey);
                assemblyContext.IsUnstripped = true;
            }
            assemblyDictionary.Add(assembly.ManifestModule!, assemblyContext);
        }

        // Inject types
        var injectedTypes = new List<(TypeDefinition, InjectedTypeAnalysisContext)>();
        var existingTypes = new List<(TypeDefinition, TypeAnalysisContext)>();
        foreach ((var module, var assemblyContext) in assemblyDictionary)
        {
            foreach (var type in module.TopLevelTypes)
            {
                var typeContext = assemblyContext.GetTypeByFullName(type.FullName);
                if (typeContext is null)
                {
                    typeContext = assemblyContext.InjectType((string?)type.Namespace ?? "", (string?)type.Name ?? "", null, (System.Reflection.TypeAttributes)type.Attributes);
                    foreach (var genericParameter in type.GenericParameters)
                    {
                        var genericParameterContext = new GenericParameterTypeAnalysisContext(
                            genericParameter.Name!,
                            genericParameter.Number,
                            Il2CppTypeEnum.IL2CPP_TYPE_VAR,
                            (System.Reflection.GenericParameterAttributes)genericParameter.Attributes,
                            typeContext);
                        typeContext.GenericParameters.Add(genericParameterContext);
                    }
                    injectedTypes.Add((type, (InjectedTypeAnalysisContext)typeContext));
                    typeContext.IsUnstripped = true;
                }
                else
                {
                    existingTypes.Add((type, typeContext));
                }
                InjectNestedTypes(type, typeContext, injectedTypes, existingTypes);
            }
        }

        // Set up type hierarchy
        foreach (var (type, typeContext) in injectedTypes)
        {
            var resolver = new ContextResolver(typeContext);
            if (type.BaseType is not null)
            {
                if (resolver.TryResolve(type.BaseType, out var baseTypeContext))
                {
                    typeContext.SetDefaultBaseType(baseTypeContext);
                }
                else
                {
                    throw new InvalidOperationException($"Unable to resolve base type {type.BaseType} for type {type.FullName}");
                }
            }

            foreach (var interfaceType in type.Interfaces.Select(t => t.Interface))
            {
                if (resolver.TryResolve(interfaceType, out var interfaceTypeContext))
                {
                    typeContext.InterfaceContexts.Add(interfaceTypeContext);
                }
                else
                {
                    throw new InvalidOperationException($"Unable to resolve interface type {interfaceType} for type {type.FullName}");
                }
            }

            for (var i = 0; i < type.GenericParameters.Count; i++)
            {
                var genericParameter = type.GenericParameters[i];
                var genericParameterContext = typeContext.GenericParameters[i];
                foreach (var constraint in genericParameter.Constraints)
                {
                    if (resolver.TryResolve(constraint.Constraint, out var constraintType))
                    {
                        genericParameterContext.ConstraintTypes.Add(constraintType);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unable to resolve generic parameter constraint {constraint.Constraint} for type {type.FullName}");
                    }
                }
            }
        }

        // Inject members
        List<(InjectedMethodAnalysisContext, IMethodDefOrRef)> methodsNeedingOverrides = new();
        List<(MethodAnalysisContext, MethodDefinition)> methodsNeedingBodies = new();
        foreach (var (type, typeContext) in injectedTypes)
        {
            CopyCustomAttributes(type, typeContext, typeContext.DeclaringAssembly);

            for (var i = 0; i < type.GenericParameters.Count; i++)
            {
                CopyCustomAttributes(type.GenericParameters[i], typeContext.GenericParameters[i], typeContext.DeclaringAssembly);
            }

            foreach (var field in type.Fields)
            {
                TryInjectField(field, typeContext);
            }

            Dictionary<MethodDefinition, MethodAnalysisContext> methodLookup = new();
            foreach (var method in type.Methods)
            {
                if (TryInjectMethod(method, typeContext, out var methodContext))
                {
                    methodLookup.Add(method, methodContext);
                    methodsNeedingBodies.Add((methodContext, method));
                }
            }

            foreach (var property in type.Properties)
            {
                TryInjectProperty(property, typeContext, methodLookup);
            }

            foreach (var @event in type.Events)
            {
                TryInjectEvent(@event, typeContext, methodLookup);
            }

            foreach (var implementation in type.MethodImplementations)
            {
                if (implementation.Body is not MethodDefinition method || implementation.Declaration is null || !methodLookup.TryGetValue(method, out var methodContext))
                    continue;

                methodsNeedingOverrides.Add(((InjectedMethodAnalysisContext)methodContext, implementation.Declaration));
            }
        }
        foreach (var (type, typeContext) in existingTypes)
        {
            // We shouldn't inject static fields.
            // Even though it's not like instance fields where state gets changed,
            // we can't guarantee that the static fields will be initialized properly.
            // However, we can still inject constant fields.
            foreach (var field in type.Fields)
            {
                if (!field.IsStatic)
                    continue; // Instance fields cannot be unstripped

                if (typeContext.Fields.Any(f => f.Name == field.Name))
                    continue; // Already present

                if (field.Constant is null)
                    continue; // Skip fields without a constant value

                TryInjectField(field, typeContext);
            }

            Dictionary<MethodDefinition, MethodAnalysisContext> methodLookup = new();
            foreach (var method in type.Methods)
            {
                var existingMethodContext = new ContextResolver(typeContext).ResolveInType(method);
                if (existingMethodContext is not null)
                {
                    methodLookup.Add(method, existingMethodContext);
                    if (method.GenericParameters.Count > 0 || type.GenericParameters.Count > 0)
                    {
                        // We inject the method if it's generic because we want to support generic instantiations
                        // that were not present in the original game. We abstain from non-generic methods because
                        // of performance, memory usage, output size, and risk of breaking the game.
                        //
                        // Counterargument: injecting as much as possible allows for more transpilers to be written.

                        methodsNeedingBodies.Add((existingMethodContext, method));
                    }
                    continue;
                }

                if (!TryInjectMethod(method, typeContext, out var methodContext))
                    continue;

                methodLookup.Add(method, methodContext);
                methodsNeedingBodies.Add((methodContext, method));

                foreach (var implementation in type.MethodImplementations)
                {
                    if (implementation.Body != method || implementation.Declaration is null)
                        continue;
                    methodsNeedingOverrides.Add((methodContext, implementation.Declaration));
                }
            }

            foreach (var property in type.Properties)
            {
                if (typeContext.Properties.Any(p => p.Name == property.Name))
                    continue; // Already present

                TryInjectProperty(property, typeContext, methodLookup);
            }

            foreach (var @event in type.Events)
            {
                if (typeContext.Events.Any(e => e.Name == @event.Name))
                    continue; // Already present

                TryInjectEvent(@event, typeContext, methodLookup);
            }
        }

        // Assign method overrides
        foreach (var (methodContext, declaration) in methodsNeedingOverrides)
        {
            var declarationContext = new ContextResolver(methodContext.DeclaringType!).Resolve(declaration);
            if (declarationContext is not null)
            {
                methodContext.OverridesList.Add((MethodAnalysisContext)declarationContext);
            }
        }

        // Assign method bodies
        if (storeMethodBodies)
        {
            var successfulCount = 0;
            foreach (var (methodContext, methodDefinition) in methodsNeedingBodies)
            {
                var successful = OriginalMethodBody.MaybeStoreOriginalMethodBody(methodDefinition, methodContext);
                if (successful)
                {
                    successfulCount++;
                }
            }

            // Report how many method bodies were successfully stored.
            Logger.InfoNewline($"Recovered the original method body for {successfulCount}/{methodsNeedingBodies.Count} attempts.", nameof(UnstripBaseProcessingLayer));
        }
    }

    private static void InjectNestedTypes(TypeDefinition declaringType, TypeAnalysisContext declaringTypeContext, List<(TypeDefinition, InjectedTypeAnalysisContext)> injectedTypes, List<(TypeDefinition, TypeAnalysisContext)> existingTypes)
    {
        foreach (var nestedType in declaringType.NestedTypes)
        {
            var nestedTypeContext = declaringTypeContext.NestedTypes.FirstOrDefault(t => t.Name == nestedType.Name);
            if (nestedTypeContext is null)
            {
                nestedTypeContext = declaringTypeContext.InjectNestedType((string?)nestedType.Name ?? "", null, (System.Reflection.TypeAttributes)nestedType.Attributes);
                foreach (var genericParameter in nestedType.GenericParameters)
                {
                    var genericParameterContext = new GenericParameterTypeAnalysisContext(
                        genericParameter.Name!,
                        genericParameter.Number,
                        Il2CppTypeEnum.IL2CPP_TYPE_VAR,
                        (System.Reflection.GenericParameterAttributes)genericParameter.Attributes,
                        nestedTypeContext);
                    nestedTypeContext.GenericParameters.Add(genericParameterContext);
                }
                injectedTypes.Add((nestedType, (InjectedTypeAnalysisContext)nestedTypeContext));

                nestedTypeContext.IsUnstripped = true;
            }
            else
            {
                existingTypes.Add((nestedType, nestedTypeContext));
            }
            InjectNestedTypes(nestedType, nestedTypeContext, injectedTypes, existingTypes);
        }
    }

    private static bool TryInjectField(FieldDefinition field, TypeAnalysisContext typeContext)
    {
        if (field.Name is not null && new ContextResolver(typeContext).TryResolve(field.Signature?.FieldType, out var fieldTypeContext))
        {
            var fieldContext = new InjectedFieldAnalysisContext(
                field.Name!,
                fieldTypeContext,
                (System.Reflection.FieldAttributes)field.Attributes,
                typeContext);
            typeContext.Fields.Add(fieldContext);

            if (field.Constant is { Type: not AsmResolver.PE.DotNet.Metadata.Tables.ElementType.Class })
            {
                fieldContext.OverrideConstantValue = field.Constant.Value?.InterpretData(field.Constant.Type);
            }
            // https://github.com/Washi1337/AsmResolver/pull/627
            //fieldContext.OverrideConstantValue = field.Constant?.InterpretData();

            CopyCustomAttributes(field, fieldContext, typeContext.DeclaringAssembly);

            fieldContext.IsUnstripped = true;

            return true;
        }

        return false;
    }

    private static bool TryInjectMethod(MethodDefinition method, TypeAnalysisContext typeContext, [NotNullWhen(true)] out InjectedMethodAnalysisContext? methodContext)
    {
        // Due to an unfortunate reality of resolving types, we need to create a method and add it to our target type before we can resolve its signature.
        // This is because the method signature can reference the method's generic parameters, so we need to create the method first.
        methodContext = new InjectedMethodAnalysisContext(
            typeContext,
            method.Name!,
            typeContext.AppContext.SystemTypes.SystemObjectType,
            (System.Reflection.MethodAttributes)method.Attributes,
            Enumerable.Repeat(typeContext.AppContext.SystemTypes.SystemObjectType, method.Parameters.Count).ToArray(),
            method.Parameters.Select(p => p.Name).ToArray(),
            method.Parameters.Select(p => (System.Reflection.ParameterAttributes)p.GetOrCreateDefinition().Attributes).ToArray(),
            (System.Reflection.MethodImplAttributes)method.ImplAttributes);

        foreach (var genericParameter in method.GenericParameters)
        {
            var genericParameterContext = new GenericParameterTypeAnalysisContext(genericParameter.Name!, genericParameter.Number, Il2CppTypeEnum.IL2CPP_TYPE_MVAR, (System.Reflection.GenericParameterAttributes)genericParameter.Attributes, methodContext);
            methodContext.GenericParameters.Add(genericParameterContext);
        }

        typeContext.Methods.Add(methodContext);

        var methodResolver = new ContextResolver(methodContext);

        if (!methodResolver.TryResolve(method.Signature?.ReturnType, out var returnTypeContext))
        {
            typeContext.Methods.Remove(methodContext);
            return false;
        }
        else
        {
            methodContext.SetDefaultReturnType(returnTypeContext);
        }

        if (!methodResolver.TryResolve(method.Parameters.Select(p => p.ParameterType), out var parameterTypeContexts))
        {
            typeContext.Methods.Remove(methodContext);
            return false;
        }
        else
        {
            for (var i = 0; i < parameterTypeContexts.Count; i++)
            {
                var parameter = (InjectedParameterAnalysisContext)methodContext.Parameters[i];
                parameter.SetDefaultParameterType(parameterTypeContexts[i]);
            }
        }

        if (!TryAddGenericConstraints(method, methodContext))
        {
            typeContext.Methods.Remove(methodContext);
            return false;
        }

        CopyCustomAttributes(method, methodContext, typeContext.DeclaringAssembly);
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            CopyCustomAttributes(method.Parameters[i].GetOrCreateDefinition(), methodContext.Parameters[i], typeContext.DeclaringAssembly);
        }
        for (var i = 0; i < method.GenericParameters.Count; i++)
        {
            CopyCustomAttributes(method.GenericParameters[i], methodContext.GenericParameters[i], typeContext.DeclaringAssembly);
        }

        methodContext.IsUnstripped = true;

        return true;

        static bool TryAddGenericConstraints(MethodDefinition method, InjectedMethodAnalysisContext methodContext)
        {
            var methodResolver = new ContextResolver(methodContext);
            var anyInvalidConstraints = false;
            for (var i = 0; i < method.GenericParameters.Count; i++)
            {
                var genericParameter = method.GenericParameters[i];
                var genericParameterContext = methodContext.GenericParameters[i];
                foreach (var constraint in genericParameter.Constraints)
                {
                    if (methodResolver.TryResolve(constraint.Constraint, out var constraintType))
                    {
                        genericParameterContext.ConstraintTypes.Add(constraintType);
                    }
                    else
                    {
                        anyInvalidConstraints = true;
                        break;
                    }
                }
                if (anyInvalidConstraints)
                    break;
            }

            return !anyInvalidConstraints;
        }
    }

    private static bool TryInjectProperty(PropertyDefinition property, TypeAnalysisContext typeContext, Dictionary<MethodDefinition, MethodAnalysisContext> methodLookup)
    {
        var resolver = new ContextResolver(typeContext);

        if (!resolver.TryResolve(property.Signature?.ReturnType, out var propertyTypeContext))
            return false;

        var getMethodContext = methodLookup.TryGetValue(property.GetMethod);
        if (getMethodContext is null && property.GetMethod is not null)
            return false;

        var setMethodContext = methodLookup.TryGetValue(property.SetMethod);
        if (setMethodContext is null && property.SetMethod is not null)
            return false;

        var propertyContext = new InjectedPropertyAnalysisContext(
            property.Name!,
            propertyTypeContext,
            getMethodContext,
            setMethodContext,
            (System.Reflection.PropertyAttributes)property.Attributes,
            typeContext);
        typeContext.Properties.Add(propertyContext);

        CopyCustomAttributes(property, propertyContext, typeContext.DeclaringAssembly);

        propertyContext.IsUnstripped = true;

        return true;
    }

    private static bool TryInjectEvent(EventDefinition @event, TypeAnalysisContext typeContext, Dictionary<MethodDefinition, MethodAnalysisContext> methodLookup)
    {
        var resolver = new ContextResolver(typeContext);

        if (!resolver.TryResolve(@event.EventType!.ToTypeSignature(), out var eventTypeContext))
            return false;

        var addMethodContext = methodLookup.TryGetValue(@event.AddMethod);
        if (addMethodContext is null && @event.AddMethod is not null)
            return false;

        var removeMethodContext = methodLookup.TryGetValue(@event.RemoveMethod);
        if (removeMethodContext is null && @event.RemoveMethod is not null)
            return false;

        var fireMethodContext = methodLookup.TryGetValue(@event.FireMethod);
        if (fireMethodContext is null && @event.FireMethod is not null)
            return false;

        var eventContext = new InjectedEventAnalysisContext(
            @event.Name!,
            eventTypeContext,
            addMethodContext,
            removeMethodContext,
            fireMethodContext,
            (System.Reflection.EventAttributes)@event.Attributes,
            typeContext);
        typeContext.Events.Add(eventContext);

        CopyCustomAttributes(@event, eventContext, typeContext.DeclaringAssembly);

        eventContext.IsUnstripped = true;

        return true;
    }

    private static void CopyCustomAttributes(IHasCustomAttribute source, HasCustomAttributes destination, AssemblyAnalysisContext assembly)
    {
        foreach (var customAttribute in source.CustomAttributes)
        {
            if (customAttribute.Constructor is null or { Signature: null or { ParameterTypes.Count: > 0 } })
                continue; // Skip custom attributes with parameters or an invalid constructor

            if (!new ContextResolver(assembly).TryResolve(customAttribute.Constructor, out var constructorContext))
                continue; // Skip custom attributes with an invalid constructor

            destination.CustomAttributes ??= [];
            destination.CustomAttributes.Add(new AnalyzedCustomAttribute((MethodAnalysisContext)constructorContext));
        }
    }

    private static bool IsEmpty(ModuleDefinition module)
    {
        return module.TopLevelTypes.Count == 0 || (module.TopLevelTypes.Count == 1 && module.TopLevelTypes[0].IsModuleType);
    }
}
