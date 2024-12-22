using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Utils;

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

    public RewriteGlobalContext(GeneratorOptions options, IIl2CppMetadataAccess gameAssemblies,
        IMetadataAccess unityAssemblies)
    {
        Options = options;
        GameAssemblies = gameAssemblies;
        UnityAssemblies = unityAssemblies;

        Il2CppAssemblyResolver assemblyResolver = new();

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

            newModule.MetadataResolver = new DefaultMetadataResolver(assemblyResolver);
            assemblyResolver.AddToCache(newAssembly);

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
        UnityAssemblies.Dispose();
    }

    internal void AddAssemblyContext(string assemblyName, AssemblyRewriteContext context)
    {
        myAssemblies[assemblyName] = context;
        if (context.OriginalAssembly != null)
            myAssembliesByOld[context.OriginalAssembly] = context;
        myAssembliesByNew[context.NewAssembly] = context;
    }

    public AssemblyRewriteContext GetNewAssemblyForOriginal(AssemblyDefinition oldAssembly)
    {
        return myAssembliesByOld[oldAssembly];
    }

    public TypeRewriteContext GetNewTypeForOriginal(TypeDefinition originalType)
    {
        return GetNewAssemblyForOriginal(originalType.Module!.Assembly!)
            .GetContextForOriginalType(originalType);
    }

    public TypeRewriteContext? TryGetNewTypeForOriginal(TypeDefinition originalType)
    {
        if (!myAssembliesByOld.TryGetValue(originalType.Module!.Assembly!, out var assembly))
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

        var fieldTypeContext = GetNewTypeForOriginal(typeRef.Resolve() ?? throw new($"Could not resolve {typeRef.FullName}"));
        return fieldTypeContext.ComputedTypeSpecifics;
    }

    public AssemblyRewriteContext GetAssemblyByName(string name)
    {
        return myAssemblies[name];
    }

    public AssemblyRewriteContext? TryGetAssemblyByName(string? name)
    {
        if (name is null)
            return null;

        if (myAssemblies.TryGetValue(name, out var result))
            return result;

        if (name == "netstandard")
            return myAssemblies.TryGetValue("mscorlib", out var result2) ? result2 : null;

        return null;
    }

    public AssemblyRewriteContext GetContextForNewAssembly(AssemblyDefinition assembly)
    {
        return myAssembliesByNew[assembly];
    }

    public TypeRewriteContext GetContextForNewType(TypeDefinition type)
    {
        return GetContextForNewAssembly(type.Module!.Assembly!).GetContextForNewType(type);
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

                        constructorReference = imports.Module.DefaultImporter.ImportMethod(convertedElementType.IsValueType
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
