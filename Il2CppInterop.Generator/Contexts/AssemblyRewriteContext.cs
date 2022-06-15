using System.Collections.Generic;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Contexts;

public class AssemblyRewriteContext
{
    // TODO: Dispose
    private static readonly Dictionary<ModuleDefinition, RuntimeAssemblyReferences> ImportsMap = new();

    public readonly RewriteGlobalContext GlobalContext;

    public readonly RuntimeAssemblyReferences Imports;
    private readonly Dictionary<string, TypeRewriteContext> myNameTypeMap = new();
    private readonly Dictionary<TypeDefinition, TypeRewriteContext> myNewTypeMap = new();

    private readonly Dictionary<TypeDefinition, TypeRewriteContext> myOldTypeMap = new();
    public readonly AssemblyDefinition NewAssembly;

    public readonly AssemblyDefinition OriginalAssembly;

    public AssemblyRewriteContext(RewriteGlobalContext globalContext, AssemblyDefinition originalAssembly,
        AssemblyDefinition newAssembly)
    {
        OriginalAssembly = originalAssembly;
        NewAssembly = newAssembly;
        GlobalContext = globalContext;

        Imports = ImportsMap.GetOrCreate(newAssembly.MainModule,
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

    public MethodReference RewriteMethodRef(MethodReference methodRef)
    {
        var newType = GlobalContext.GetNewTypeForOriginal(methodRef.DeclaringType.Resolve());
        return newType.GetMethodByOldMethod(methodRef.Resolve()).NewMethod;
    }

    public TypeReference RewriteTypeRef(TypeReference? typeRef)
    {
        if (typeRef == null) return Imports.Il2CppObjectBase;

        var sourceModule = NewAssembly.MainModule;

        if (typeRef is ArrayType arrayType)
        {
            if (arrayType.Rank != 1)
                return Imports.Il2CppObjectBase;

            var elementType = arrayType.ElementType;
            if (elementType.FullName == "System.String")
                return Imports.Il2CppStringArray;

            var convertedElementType = RewriteTypeRef(elementType);
            if (elementType.IsGenericParameter)
                return new GenericInstanceType(Imports.Il2CppArrayBase) { GenericArguments = { convertedElementType } };

            return new GenericInstanceType(convertedElementType.IsValueType
                    ? Imports.Il2CppStructArray
                    : Imports.Il2CppReferenceArray)
            { GenericArguments = { convertedElementType } };
        }

        if (typeRef is GenericParameter genericParameter)
        {
            var genericParameterDeclaringType = genericParameter.DeclaringType;
            if (genericParameterDeclaringType != null)
                return RewriteTypeRef(genericParameterDeclaringType).GenericParameters[genericParameter.Position];

            return RewriteMethodRef(genericParameter.DeclaringMethod).GenericParameters[genericParameter.Position];
        }

        if (typeRef is ByReferenceType byRef)
            return new ByReferenceType(RewriteTypeRef(byRef.ElementType));

        if (typeRef is PointerType pointerType)
            return new PointerType(RewriteTypeRef(pointerType.ElementType));

        if (typeRef is GenericInstanceType genericInstance)
        {
            var newRef = new GenericInstanceType(RewriteTypeRef(genericInstance.ElementType));
            foreach (var originalParameter in genericInstance.GenericArguments)
                newRef.GenericArguments.Add(RewriteTypeRef(originalParameter));

            return newRef;
        }

        if (typeRef.IsPrimitive || typeRef.FullName == "System.TypedReference")
            return sourceModule.ImportCorlibReference(typeRef.Namespace, typeRef.Name);

        if (typeRef.FullName == "System.Void")
            return Imports.Module.Void();

        if (typeRef.FullName == "System.String")
            return Imports.Module.String();

        if (typeRef.FullName == "System.Object")
            return sourceModule.ImportReference(GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Object").NewType);

        if (typeRef.FullName == "System.Attribute")
            return sourceModule.ImportReference(GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Attribute").NewType);

        var originalTypeDef = typeRef.Resolve();
        var targetAssembly = GlobalContext.GetNewAssemblyForOriginal(originalTypeDef.Module.Assembly);
        var target = targetAssembly.GetContextForOriginalType(originalTypeDef).NewType;

        return sourceModule.ImportReference(target);
    }

    public TypeRewriteContext GetTypeByName(string name)
    {
        return myNameTypeMap[name];
    }

    public TypeRewriteContext? TryGetTypeByName(string name)
    {
        return myNameTypeMap.TryGetValue(name, out var result) ? result : null;
    }
}
