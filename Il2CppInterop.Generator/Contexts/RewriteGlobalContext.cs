using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Contexts;

public class RewriteGlobalContext : IDisposable
{
    internal readonly List<long> MethodStartAddresses = new();

    private readonly Dictionary<string, AssemblyRewriteContext> myAssemblies = new();
    private readonly Dictionary<AssemblyDefinition, AssemblyRewriteContext> myAssembliesByOld = new();
    private readonly Dictionary<AssemblyDefinition, AssemblyRewriteContext> myAssembliesByNew = new();
    internal readonly Dictionary<TypeDefinition, string> PreviousRenamedTypes = new();
    internal readonly Dictionary<TypeDefinition, string> RenamedTypes = new();

    internal readonly Dictionary<(object?, string, int), List<TypeDefinition>> RenameGroups = new();

    internal readonly Dictionary<ModuleDefinition, RuntimeAssemblyReferences> ImportsMap = new();

    // Reference-only assemblies loaded from ExistingInteropDir for incremental mode
    private readonly Dictionary<string, AssemblyRewriteContext> myReferenceAssemblies = new();

    public RewriteGlobalContext(GeneratorOptions options, IIl2CppMetadataAccess gameAssemblies,
        IMetadataAccess unityAssemblies)
    {
        Options = options;
        GameAssemblies = gameAssemblies;
        UnityAssemblies = unityAssemblies;

        Il2CppAssemblyResolver assemblyResolver = new();

        // In incremental mode, load existing interop assemblies as references first
        if (!string.IsNullOrEmpty(options.ExistingInteropDir) && Directory.Exists(options.ExistingInteropDir))
        {
            LoadExistingInteropAssemblies(options.ExistingInteropDir!, assemblyResolver);
        }

        foreach (var sourceAssembly in gameAssemblies.Assemblies)
        {
            var assemblyName = sourceAssembly.Name!;
            if (assemblyName == "Il2CppDummyDll")
            {
                continue;
            }

            var newAssembly = new AssemblyDefinition(sourceAssembly.Name.UnSystemify(options), sourceAssembly.Version);
            var newModule = new ModuleDefinition(sourceAssembly.ManifestModule?.Name.UnSystemify(options), CorlibReferences.TargetCorlib);
            newAssembly.Modules.Add(newModule);

            // Use HybridCLR-aware metadata resolver that handles type relocation
            newModule.MetadataResolver = new HybridCLRMetadataResolver(assemblyResolver);
            assemblyResolver.AddToCache(newAssembly);

            var assemblyRewriteContext = new AssemblyRewriteContext(this, sourceAssembly, newAssembly);
            AddAssemblyContext(assemblyName, assemblyRewriteContext);
        }
    }

    /// <summary>
    /// Loads existing interop assemblies from ExistingInteropDir as reference-only contexts.
    /// These are used to resolve types like System.Type when generating hotfix interop.
    /// </summary>
    private void LoadExistingInteropAssemblies(string existingInteropDir, Il2CppAssemblyResolver assemblyResolver)
    {
        Logger.Instance.LogInformation("Loading existing interop assemblies from {Dir}", existingInteropDir);
        int loadedCount = 0;

        foreach (var dllPath in Directory.EnumerateFiles(existingInteropDir, "*.dll"))
        {
            try
            {
                var assembly = AssemblyDefinition.FromFile(dllPath);
                if (assembly?.ManifestModule == null)
                    continue;

                // The assembly name in the file (e.g., "Il2Cppmscorlib")
                var assemblyName = assembly.Name?.Value ?? Path.GetFileNameWithoutExtension(dllPath);

                // Create a reference-only context (no original assembly, just the new/interop assembly)
                var context = new AssemblyRewriteContext(this, null!, assembly);

                // Register types from the existing interop assembly
                foreach (var type in assembly.ManifestModule.TopLevelTypes)
                {
                    RegisterExistingInteropType(context, type);
                }

                // Register under the assembly name as it appears in the file
                myReferenceAssemblies[assemblyName] = context;

                // Also register under the original name (without Il2Cpp prefix) for lookup compatibility
                // e.g., "Il2Cppmscorlib" should also be findable as "mscorlib"
                if (assemblyName.StartsWith("Il2Cpp"))
                {
                    var originalName = assemblyName.Substring(6);
                    if (!myReferenceAssemblies.ContainsKey(originalName))
                    {
                        myReferenceAssemblies[originalName] = context;
                    }
                }

                assemblyResolver.AddToCache(assembly);
                loadedCount++;

                Logger.Instance.LogTrace("Loaded reference assembly: {Name}", assemblyName);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("Failed to load reference assembly {Path}: {Error}",
                    Path.GetFileName(dllPath), ex.Message);
            }
        }

        Logger.Instance.LogInformation("Loaded {Count} reference assemblies from ExistingInteropDir", loadedCount);
    }

    private void RegisterExistingInteropType(AssemblyRewriteContext context, TypeDefinition type)
    {
        // Create a TypeRewriteContext for the existing type (reference-only, no original)
        var typeContext = new TypeRewriteContext(context, null, type);
        context.RegisterTypeRewrite(typeContext);

        // Also register under the original (non-Il2Cpp-prefixed) name for lookup compatibility
        // e.g., "Il2CppSystem.Type" should also be findable as "System.Type"
        var fullName = type.FullName;
        if (fullName != null)
        {
            // Handle Il2Cpp prefix in namespace
            if (fullName.StartsWith("Il2Cpp"))
            {
                var originalName = fullName.Substring(6); // Remove "Il2Cpp" prefix
                context.RegisterTypeByAlternativeName(originalName, typeContext);
            }
        }

        // Register nested types
        foreach (var nestedType in type.NestedTypes)
        {
            RegisterExistingInteropType(context, nestedType);
        }
    }

    public GeneratorOptions Options { get; }
    public IIl2CppMetadataAccess GameAssemblies { get; }
    public IMetadataAccess UnityAssemblies { get; }

    public IEnumerable<AssemblyRewriteContext> Assemblies => myAssemblies.Values;

    /// <summary>
    /// Gets the mscorlib assembly context. Returns null if mscorlib is not loaded.
    /// Checks both source assemblies and reference assemblies (from ExistingInteropDir).
    /// </summary>
    public AssemblyRewriteContext? CorLib =>
        myAssemblies.TryGetValue("mscorlib", out var corlib) ? corlib :
        myReferenceAssemblies.TryGetValue("mscorlib", out var refCorlib) ? refCorlib : null;

    internal bool HasGcWbarrierFieldWrite { get; set; }

    public void Dispose()
    {
        UnityAssemblies.Dispose();
    }

    internal void AddAssemblyContext(string assemblyName, AssemblyRewriteContext context)
    {
        myAssemblies[assemblyName] = context;
        if (context.OriginalAssembly != null)
            myAssembliesByOld[context.OriginalAssembly] = context;
        myAssembliesByNew[context.NewAssembly] = context;
    }

    public AssemblyRewriteContext? GetNewAssemblyForOriginal(AssemblyDefinition? oldAssembly)
    {
        if (oldAssembly == null) return null;
        return myAssembliesByOld.TryGetValue(oldAssembly, out var result) ? result : null;
    }

    public TypeRewriteContext? GetNewTypeForOriginal(TypeDefinition? originalType)
    {
        if (originalType?.Module?.Assembly == null) return null;
        var assembly = GetNewAssemblyForOriginal(originalType.Module.Assembly);
        return assembly?.TryGetContextForOriginalType(originalType);
    }

    public TypeRewriteContext? TryGetNewTypeForOriginal(TypeDefinition? originalType)
    {
        if (originalType?.Module?.Assembly == null) return null;
        if (!myAssembliesByOld.TryGetValue(originalType.Module.Assembly, out var assembly))
            return null;
        return assembly.TryGetContextForOriginalType(originalType);
    }

    public TypeRewriteContext.TypeSpecifics JudgeSpecificsByOriginalType(TypeSignature typeRef)
    {
        if (typeRef.IsPrimitive() || typeRef is PointerTypeSignature || typeRef.FullName == "System.TypedReference")
            return TypeRewriteContext.TypeSpecifics.BlittableStruct;
        if (typeRef
            is CorLibTypeSignature { ElementType: ElementType.String or ElementType.Object }
            or ArrayBaseTypeSignature
            or ByReferenceTypeSignature
            or GenericParameterSignature
            or GenericInstanceTypeSignature)
            return TypeRewriteContext.TypeSpecifics.ReferenceType;

        var resolved = typeRef.Resolve();
        if (resolved == null)
            return TypeRewriteContext.TypeSpecifics.ReferenceType; // Fallback for unresolvable types

        var fieldTypeContext = GetNewTypeForOriginal(resolved);
        if (fieldTypeContext == null)
            return TypeRewriteContext.TypeSpecifics.ReferenceType; // Fallback for missing types

        return fieldTypeContext.ComputedTypeSpecifics;
    }

    public AssemblyRewriteContext GetAssemblyByName(string name)
    {
        if (myAssemblies.TryGetValue(name, out var result))
            return result;

        // Fall back to reference assemblies (from ExistingInteropDir)
        if (myReferenceAssemblies.TryGetValue(name, out var refResult))
            return refResult;

        throw new KeyNotFoundException($"Assembly '{name}' not found in source or reference assemblies");
    }

    public AssemblyRewriteContext? TryGetAssemblyByName(string? name)
    {
        if (name is null)
            return null;

        if (myAssemblies.TryGetValue(name, out var result))
            return result;

        // Fall back to reference assemblies (from ExistingInteropDir)
        if (myReferenceAssemblies.TryGetValue(name, out var refResult))
            return refResult;

        if (name == "netstandard")
        {
            if (myAssemblies.TryGetValue("mscorlib", out var result2))
                return result2;
            if (myReferenceAssemblies.TryGetValue("mscorlib", out var refResult2))
                return refResult2;
        }

        return null;
    }

    public AssemblyRewriteContext? GetContextForNewAssembly(AssemblyDefinition? assembly)
    {
        if (assembly == null) return null;
        if (myAssembliesByNew.TryGetValue(assembly, out var result))
            return result;

        // Check reference assemblies (they are stored by NewAssembly, not in myAssembliesByNew)
        foreach (var refAsm in myReferenceAssemblies.Values)
        {
            if (refAsm.NewAssembly == assembly)
                return refAsm;
        }

        return null;
    }

    public TypeRewriteContext? GetContextForNewType(TypeDefinition? type)
    {
        if (type?.Module?.Assembly == null) return null;
        return GetContextForNewAssembly(type.Module.Assembly)?.GetContextForNewType(type);
    }

    public MethodDefinition? CreateParamsMethod(MethodDefinition originalMethod, MethodDefinition newMethod,
        RuntimeAssemblyReferences imports, Func<TypeSignature, TypeSignature?> resolve)
    {
        if (newMethod.Name == "Invoke")
            return null;

        var paramsParameters = originalMethod.Parameters.Where(parameter =>
            parameter.IsParamsArray() && resolve(((ArrayBaseTypeSignature)parameter.ParameterType).BaseType) is not null and not GenericParameterSignature
        ).ToArray();

        if (paramsParameters.Any())
        {
            var paramsMethod = new MethodDefinition(newMethod.Name, newMethod.Attributes, MethodSignatureCreator.CreateMethodSignature(newMethod.Attributes, newMethod.Signature!.ReturnType, newMethod.Signature.GenericParameterCount));
            foreach (var genericParameter in originalMethod.GenericParameters)
            {
                var newGenericParameter = new GenericParameter(genericParameter.Name.MakeValidInSource(), genericParameter.Attributes);

                foreach (var constraint in genericParameter.Constraints)
                {
                    var newConstraintType = constraint.Constraint != null ? resolve(constraint.Constraint.ToTypeSignature())?.ToTypeDefOrRef() : null;
                    var newConstraint = new GenericParameterConstraint(newConstraintType);

                    // We don't need to copy custom attributes on constraints for generic parameters because Il2Cpp doesn't support them.

                    newGenericParameter.Constraints.Add(newConstraint);
                }

                // Similarly, custom attributes on generic parameters are also stripped by Il2Cpp, so we don't need to copy them.

                paramsMethod.GenericParameters.Add(newGenericParameter);
            }

            foreach (var originalParameter in originalMethod.Parameters)
            {
                var isParams = paramsParameters.Contains(originalParameter);

                TypeSignature? convertedType;
                if (isParams && originalParameter.ParameterType is ArrayBaseTypeSignature arrayType)
                {
                    var resolvedElementType = resolve(arrayType.GetElementType());
                    convertedType = arrayType is SzArrayTypeSignature
                        ? resolvedElementType?.MakeSzArrayType()
                        : resolvedElementType?.MakeArrayType(arrayType.Rank);
                }
                else
                {
                    convertedType = resolve(originalParameter.ParameterType);
                }

                if (convertedType == null)
                {
                    throw new($"Could not resolve parameter type {originalParameter.ParameterType.FullName}");
                }

                var parameter = paramsMethod.AddParameter(convertedType, originalParameter.Name, originalParameter.Definition?.Attributes ?? default);

                if (isParams)
                    parameter.Definition!.CustomAttributes.Add(new CustomAttribute(newMethod.Module!.ParamArrayAttributeCtor()));
            }

            paramsMethod.CilMethodBody = new(paramsMethod);
            var body = paramsMethod.CilMethodBody.Instructions;

            if (!newMethod.IsStatic)
            {
                body.Add(OpCodes.Ldarg_0);
            }

            for (var i = 0; i < newMethod.Parameters.Count; i++)
            {
                body.Add(OpCodes.Ldarg, newMethod.Parameters[i]);

                var parameter = originalMethod.Parameters[i];
                if (paramsParameters.Contains(parameter))
                {
                    var parameterType = (ArrayBaseTypeSignature)parameter.ParameterType;

                    IMethodDescriptor constructorReference;

                    var elementType = parameterType.GetElementType();
                    if (elementType.FullName == "System.String")
                    {
                        constructorReference = imports.Il2CppStringArrayctor.Value;
                    }
                    else
                    {
                        var convertedElementType = resolve(elementType)!;

                        constructorReference = imports.Module.DefaultImporter.ImportMethod(convertedElementType.IsValueType()
                            ? imports.Il2CppStructArrayctor.Get(convertedElementType)
                            : imports.Il2CppRefrenceArrayctor.Get(convertedElementType));
                    }

                    body.Add(OpCodes.Newobj, constructorReference);
                }
            }

            body.Add(OpCodes.Call, newMethod);
            body.Add(OpCodes.Ret);

            return paramsMethod;
        }

        return null;
    }
}
