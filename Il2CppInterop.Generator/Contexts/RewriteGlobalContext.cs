using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Contexts;

public class RewriteGlobalContext : IDisposable
{
    internal readonly List<long> MethodStartAddresses = new();

    private readonly Dictionary<string, AssemblyRewriteContext> myAssemblies = new();
    private readonly Dictionary<AssemblyDefinition, AssemblyRewriteContext> myAssembliesByOld = new();
    internal readonly Dictionary<TypeDefinition, string> PreviousRenamedTypes = new();
    internal readonly Dictionary<TypeDefinition, string> RenamedTypes = new();

    internal readonly Dictionary<(object, string, int), List<TypeDefinition>> RenameGroups = new();

    public RewriteGlobalContext(GeneratorOptions options, IIl2CppMetadataAccess gameAssemblies,
        IMetadataAccess unityAssemblies)
    {
        Options = options;
        GameAssemblies = gameAssemblies;
        UnityAssemblies = unityAssemblies;

        foreach (var sourceAssembly in gameAssemblies.Assemblies)
        {
            var assemblyName = sourceAssembly.Name.Name;
            if (assemblyName == "Il2CppDummyDll")
            {
                sourceAssembly.Dispose();
                continue;
            }

            var newAssembly = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(sourceAssembly.Name.Name.UnSystemify(options), sourceAssembly.Name.Version),
                sourceAssembly.MainModule.Name.UnSystemify(options), sourceAssembly.MainModule.Kind);

            var assemblyRewriteContext = new AssemblyRewriteContext(this, sourceAssembly, newAssembly);
            AddAssemblyContext(assemblyName, assemblyRewriteContext);
        }
    }

    public GeneratorOptions Options { get; }
    public IIl2CppMetadataAccess GameAssemblies { get; }
    public IMetadataAccess UnityAssemblies { get; }

    public IEnumerable<AssemblyRewriteContext> Assemblies => myAssemblies.Values;

    internal bool HasGcWbarrierFieldWrite { get; set; }

    public void Dispose()
    {
        var resolverCacheField =
            typeof(DefaultAssemblyResolver).GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var assembly in Assemblies)
        {
            foreach (var module in assembly.NewAssembly.Modules)
            {
                var resolver = (DefaultAssemblyResolver)module.AssemblyResolver;
                var cache = (Dictionary<string, AssemblyDefinition>)resolverCacheField!.GetValue(resolver);
                cache.Clear();
            }

            assembly.NewAssembly.Dispose();
            assembly.OriginalAssembly.Dispose();
        }

        UnityAssemblies.Dispose();
    }

    internal void AddAssemblyContext(string assemblyName, AssemblyRewriteContext context)
    {
        myAssemblies[assemblyName] = context;
        if (context.OriginalAssembly != null)
            myAssembliesByOld[context.OriginalAssembly] = context;
    }

    public AssemblyRewriteContext GetNewAssemblyForOriginal(AssemblyDefinition oldAssembly)
    {
        try
        {
            return myAssembliesByOld[oldAssembly];
        }
        catch
        {
            foreach (var assembly in myAssembliesByOld.Keys)
            {
                if (assembly.Name == oldAssembly.Name)
                    return myAssembliesByOld[assembly];
            }
            return myAssemblies.TryGetValue("Il2Cppmscorlib", out var result2) ? result2 : null;
        }
    }

    public TypeRewriteContext GetNewTypeForOriginal(TypeDefinition originalType)
    {
        return (GetNewAssemblyForOriginal(originalType.Module.Assembly) ??
                 (myAssemblies.TryGetValue("Il2Cppmscorlib", out var result1) ?
                 result1 :
                 null))?
                 .GetContextForOriginalType(originalType) ??
                 (myAssemblies.TryGetValue("Il2Cppmscorlib", out var result2) ?
                 result2.GetContextForOriginalType(originalType) :
                 null);
    }

    public TypeRewriteContext? TryGetNewTypeForOriginal(TypeDefinition originalType)
    {
        if (!myAssembliesByOld.TryGetValue(originalType.Module.Assembly, out var assembly))
            return null;
        return assembly.TryGetContextForOriginalType(originalType);
    }

    public TypeRewriteContext.TypeSpecifics JudgeSpecificsByOriginalType(TypeReference typeRef)
    {
        if (typeRef.IsPrimitive || typeRef.IsPointer || typeRef.FullName == "System.TypedReference")
            return TypeRewriteContext.TypeSpecifics.BlittableStruct;
        if (typeRef.FullName == "System.String" || typeRef.FullName == "System.Object" || typeRef.IsArray ||
            typeRef.IsByReference || typeRef.IsGenericParameter || typeRef.IsGenericInstance)
            return TypeRewriteContext.TypeSpecifics.ReferenceType;

        var fieldTypeContext = GetNewTypeForOriginal(typeRef.Resolve());
        return fieldTypeContext != null ? fieldTypeContext.ComputedTypeSpecifics : TypeRewriteContext.TypeSpecifics.NotComputed;
    }

    public AssemblyRewriteContext GetAssemblyByName(string name)
    {
        return myAssemblies.TryGetValue(name, out var result1) ?
                result1 :
                myAssemblies.TryGetValue("mscorlib", out var result2) ?
                result2 :
                myAssemblies.TryGetValue("Il2Cppmscorlib", out var result3) ?
                result3 :
                null;
    }

    public AssemblyRewriteContext? TryGetAssemblyByName(string name)
    {
        if (myAssemblies.TryGetValue(name, out var result))
            return result;

        if (name == "netstandard")
            return myAssemblies.TryGetValue("mscorlib", out var result2) ? result2 : null;

        return null;
    }

    public MethodDefinition? CreateParamsMethod(MethodDefinition originalMethod, MethodDefinition newMethod,
        RuntimeAssemblyReferences imports, Func<TypeReference, TypeReference?> resolve)
    {
        if (newMethod.Name == "Invoke")
            return null;

        var paramsParameters = originalMethod.Parameters.Where(parameter =>
            parameter.IsParamsArray() && !(resolve(((ArrayType)parameter.ParameterType).ElementType)?.IsGenericParameter ?? true)
        ).ToArray();

        if (paramsParameters.Any())
        {
            var paramsMethod = new MethodDefinition(newMethod.Name, newMethod.Attributes, newMethod.ReturnType);
            foreach (var genericParameter in newMethod.GenericParameters)
                paramsMethod.GenericParameters.Add(genericParameter);

            foreach (var originalParameter in originalMethod.Parameters)
            {
                var isParams = paramsParameters.Contains(originalParameter);

                TypeReference? convertedType;
                if (isParams && originalParameter.ParameterType is ArrayType arrayType)
                {
                    var resolvedElementType = resolve(arrayType.GetElementType());
                    convertedType = resolvedElementType == null
                        ? null
                        : new ArrayType(resolvedElementType, arrayType.Rank);
                }
                else
                {
                    convertedType = resolve(originalParameter.ParameterType);
                }

                var parameter =
                    new ParameterDefinition(originalParameter.Name, originalParameter.Attributes, convertedType);

                if (isParams)
                    parameter.CustomAttributes.Add(new CustomAttribute(newMethod.Module.ParamArrayAttributeCtor()));

                paramsMethod.Parameters.Add(parameter);
            }

            var body = paramsMethod.Body.GetILProcessor();

            if (newMethod.HasThis) body.Emit(OpCodes.Ldarg_0);

            var argOffset = newMethod.HasThis ? 1 : 0;

            for (var i = 0; i < newMethod.Parameters.Count; i++)
            {
                body.Emit(OpCodes.Ldarg, argOffset + i);

                var parameter = originalMethod.Parameters[i];
                if (paramsParameters.Contains(parameter))
                {
                    var parameterType = (ArrayType)parameter.ParameterType;

                    MethodReference constructorReference;

                    var elementType = parameterType.GetElementType();
                    if (elementType.FullName == "System.String")
                    {
                        constructorReference = imports.Il2CppStringArrayctor.Value;
                    }
                    else
                    {
                        var convertedElementType = resolve(elementType)!;

                        constructorReference = imports.Module.ImportReference(convertedElementType.IsValueType
                            ? imports.Il2CppStructArrayctor.Get(convertedElementType)
                            : imports.Il2CppRefrenceArrayctor.Get(convertedElementType));
                    }

                    body.Emit(OpCodes.Newobj, constructorReference);
                }
            }

            body.Emit(OpCodes.Call, newMethod);
            body.Emit(OpCodes.Ret);

            return paramsMethod;
        }

        return null;
    }
}
