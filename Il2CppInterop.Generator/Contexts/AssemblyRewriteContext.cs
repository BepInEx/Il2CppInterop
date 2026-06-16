using System.Diagnostics;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Contexts;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class AssemblyRewriteContext
{
    public readonly RewriteGlobalContext GlobalContext;

    public readonly RuntimeAssemblyReferences Imports;
    private readonly Dictionary<string, TypeRewriteContext> myNameTypeMap = new();
    private readonly Dictionary<TypeDefinition, TypeRewriteContext> myNewTypeMap = new();

    private readonly Dictionary<TypeDefinition, TypeRewriteContext> myOldTypeMap = new();
    public readonly AssemblyDefinition NewAssembly;

#nullable disable
    // OriginalAssembly is null for reference-only contexts (loaded from ExistingInteropDir)
    public readonly AssemblyDefinition OriginalAssembly;
#nullable enable

    public AssemblyRewriteContext(RewriteGlobalContext globalContext, AssemblyDefinition? originalAssembly,
        AssemblyDefinition newAssembly)
    {
        OriginalAssembly = originalAssembly;
        NewAssembly = newAssembly;
        GlobalContext = globalContext;

        Imports = globalContext.ImportsMap.GetOrCreate(newAssembly.ManifestModule!,
            mod => new RuntimeAssemblyReferences(mod, globalContext));
    }

    public IEnumerable<TypeRewriteContext> Types => myOldTypeMap.Values;

    public TypeRewriteContext GetContextForOriginalType(TypeDefinition type)
    {
        return myOldTypeMap[type];
    }

    public TypeRewriteContext? TryGetContextForOriginalType(TypeDefinition type)
    {
        return myOldTypeMap.TryGetValue(type, out var result) ? result : null;
    }

    public TypeRewriteContext GetContextForNewType(TypeDefinition type)
    {
        return myNewTypeMap[type];
    }

    public void RegisterTypeRewrite(TypeRewriteContext context)
    {
        if (context.OriginalType != null)
            myOldTypeMap[context.OriginalType] = context;
        myNewTypeMap[context.NewType] = context;
        myNameTypeMap[(context.OriginalType ?? context.NewType).FullName] = context;
    }

    /// <summary>
    /// Registers a type under an alternative name for lookup purposes.
    /// Used for reference-only assemblies where Il2Cpp-prefixed types need to be
    /// findable by their original names (e.g., "System.Type" for "Il2CppSystem.Type").
    /// </summary>
    public void RegisterTypeByAlternativeName(string alternativeName, TypeRewriteContext context)
    {
        if (!myNameTypeMap.ContainsKey(alternativeName))
        {
            myNameTypeMap[alternativeName] = context;
        }
    }

    public IMethodDefOrRef? RewriteMethodRef(IMethodDefOrRef? methodRef)
    {
        if (methodRef?.DeclaringType == null)
        {
            if (GlobalContext.Options.IsHybridCLREnvironment)
                return null;

            throw new ArgumentNullException(nameof(methodRef));
        }

        var declaringType = methodRef.DeclaringType.Resolve();
        if (declaringType == null)
        {
            if (GlobalContext.Options.IsHybridCLREnvironment)
                return null;

            throw new($"Could not resolve declaring type {methodRef.DeclaringType.FullName} for method {methodRef.Name}");
        }

        var newType = GlobalContext.GetNewTypeForOriginal(declaringType);
        if (newType == null)
        {
            if (GlobalContext.Options.IsHybridCLREnvironment)
                return null;

            throw new($"Could not find rewrite context for declaring type {declaringType.FullName}");
        }

        var resolvedMethod = methodRef.Resolve();
        if (resolvedMethod == null)
        {
            if (GlobalContext.Options.IsHybridCLREnvironment)
                return null;

            throw new($"Could not resolve method {methodRef.FullName}");
        }

        var methodContext = newType.TryGetMethodByOldMethod(resolvedMethod);
        if (methodContext == null)
        {
            if (GlobalContext.Options.IsHybridCLREnvironment)
                return null;

            throw new($"Could not find rewrite context for method {resolvedMethod.FullName}");
        }

        return NewAssembly.ManifestModule!.DefaultImporter.ImportMethod(methodContext.NewMethod);
    }

    public ITypeDefOrRef RewriteTypeRef(ITypeDescriptor typeRef)
    {
        return RewriteTypeRef(typeRef?.ToTypeSignature()).ToTypeDefOrRef();
    }

    public TypeSignature RewriteTypeRef(TypeSignature? typeRef)
    {
        if (typeRef == null)
            return Imports.Il2CppObjectBase;

        var sourceModule = NewAssembly.ManifestModule!;

        if (typeRef is ArrayBaseTypeSignature arrayType)
        {
            if (arrayType.Rank != 1)
                return Imports.Il2CppObjectBase;

            var elementType = arrayType.BaseType;
            if (elementType.FullName == "System.String")
                return Imports.Il2CppStringArray;

            var convertedElementType = RewriteTypeRef(elementType);
            if (elementType is GenericParameterSignature)
                return new GenericInstanceTypeSignature(Imports.Il2CppArrayBase.ToTypeDefOrRef(), false, convertedElementType);

            return new GenericInstanceTypeSignature(convertedElementType.IsValueType()
                    ? Imports.Il2CppStructArray.ToTypeDefOrRef()
                    : Imports.Il2CppReferenceArray.ToTypeDefOrRef(), false, convertedElementType);
        }

        if (typeRef is GenericParameterSignature genericParameter)
        {
            return new GenericParameterSignature(sourceModule, genericParameter.ParameterType, genericParameter.Index);
        }

        if (typeRef is ByReferenceTypeSignature byRef)
            return new ByReferenceTypeSignature(RewriteTypeRef(byRef.BaseType));

        if (typeRef is PointerTypeSignature pointerType)
            return new PointerTypeSignature(RewriteTypeRef(pointerType.BaseType));

        if (typeRef is GenericInstanceTypeSignature genericInstance)
        {
            var genericType = RewriteTypeRef(genericInstance.GenericType.ToTypeSignature()).ToTypeDefOrRef();
            var newRef = new GenericInstanceTypeSignature(genericType, genericType.IsValueType());
            foreach (var originalParameter in genericInstance.TypeArguments)
                newRef.TypeArguments.Add(RewriteTypeRef(originalParameter));

            return newRef;
        }

        if (typeRef.IsPrimitive() || typeRef.FullName == "System.TypedReference")
            return sourceModule.ImportCorlibReference(typeRef.FullName);

        if (typeRef.FullName == "System.Void")
            return Imports.Module.Void();

        if (typeRef.FullName == "System.String")
            return Imports.Module.String();

        if (typeRef.FullName == "System.Object")
        {
            var mscorlib = GlobalContext.TryGetAssemblyByName("mscorlib");
            if (mscorlib != null)
                return sourceModule.DefaultImporter.ImportType(mscorlib.GetTypeByName("System.Object").NewType).ToTypeSignature();
            if (!GlobalContext.Options.IsHybridCLREnvironment)
                throw new KeyNotFoundException("Required corlib type 'System.Object' was not found.");
            return sourceModule.CorLibTypeFactory.Object;
        }

        if (typeRef.FullName == "System.Attribute")
        {
            var mscorlib = GlobalContext.TryGetAssemblyByName("mscorlib");
            if (mscorlib != null)
                return sourceModule.DefaultImporter.ImportType(mscorlib.GetTypeByName("System.Attribute").NewType).ToTypeSignature();
            if (!GlobalContext.Options.IsHybridCLREnvironment)
                throw new KeyNotFoundException("Required corlib type 'System.Attribute' was not found.");
            return sourceModule.ImportCorlibReference("System.Attribute");
        }

        var originalTypeDef = typeRef.Resolve();
        if (originalTypeDef == null)
        {
            if (!GlobalContext.Options.IsHybridCLREnvironment)
                throw new($"Could not resolve type {typeRef.FullName}");

            var mscorlib = GlobalContext.TryGetAssemblyByName("mscorlib");
            if (mscorlib != null)
                return sourceModule.DefaultImporter.ImportType(mscorlib.GetTypeByName("System.Object").NewType).ToTypeSignature();
            return sourceModule.CorLibTypeFactory.Object;
        }

        var targetAssembly = GlobalContext.GetNewAssemblyForOriginal(originalTypeDef.DeclaringModule?.Assembly);
        if (targetAssembly == null)
        {
            // Not a source assembly — try name-based lookup to find reference (existing interop) assembly.
            // The resolved TypeDefinition lives in the raw dependency DLL (e.g., mscorlib),
            // but the registered context is for the interop DLL (e.g., Il2Cppmscorlib).
            var asmName = originalTypeDef.DeclaringModule?.Assembly?.Name?.Value;
            if (asmName != null)
                targetAssembly = GlobalContext.TryGetAssemblyByName(asmName);
        }
        if (targetAssembly == null)
        {
            if (!GlobalContext.Options.IsHybridCLREnvironment)
                throw new KeyNotFoundException(
                    $"Could not find target assembly for type {originalTypeDef.FullName} from assembly {originalTypeDef.DeclaringModule?.Assembly?.Name}");

            var mscorlib = GlobalContext.TryGetAssemblyByName("mscorlib");
            if (mscorlib != null)
                return sourceModule.DefaultImporter.ImportType(mscorlib.GetTypeByName("System.Object").NewType).ToTypeSignature();
            return sourceModule.CorLibTypeFactory.Object;
        }

        var typeContext = targetAssembly.TryGetContextForOriginalType(originalTypeDef);
        if (typeContext == null)
        {
            // For reference assemblies, the type IS the new type (no original/new distinction).
            // Object-identity lookup won't work because originalTypeDef is from the raw dependency DLL,
            // not from the interop DLL. Use name-based lookup instead.
            var typeName = originalTypeDef.FullName;
            typeContext = targetAssembly.TryGetTypeByName(typeName);
        }
        if (typeContext == null)
        {
            if (!GlobalContext.Options.IsHybridCLREnvironment)
                throw new KeyNotFoundException(
                    $"Could not find rewrite context for type {originalTypeDef.FullName} in assembly {targetAssembly.NewAssembly.Name}");

            var mscorlib = GlobalContext.TryGetAssemblyByName("mscorlib");
            if (mscorlib != null)
                return sourceModule.DefaultImporter.ImportType(mscorlib.GetTypeByName("System.Object").NewType).ToTypeSignature();
            return sourceModule.CorLibTypeFactory.Object;
        }

        return sourceModule.DefaultImporter.ImportType(typeContext.NewType).ToTypeSignature();
    }

    public TypeRewriteContext GetTypeByName(string name)
    {
        return myNameTypeMap[name];
    }

    public TypeRewriteContext? TryGetTypeByName(string name)
    {
        return myNameTypeMap.TryGetValue(name, out var result) ? result : null;
    }

    private string GetDebuggerDisplay()
    {
        return NewAssembly.FullName;
    }
}
