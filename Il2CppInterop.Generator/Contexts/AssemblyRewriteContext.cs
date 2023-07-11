using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Contexts;

public class AssemblyRewriteContext
{
    // TODO: Dispose
    private static readonly Dictionary<ModuleDefinition, RuntimeAssemblyReferences> ImportsMap = new();

    public readonly RewriteGlobalContext GlobalContext;

    public readonly RuntimeAssemblyReferences Imports;
    private readonly Dictionary<string, TypeRewriteContext> myNameTypeMap = new();
    private readonly Dictionary<TypeDefinition, TypeRewriteContext> myNewTypeMap = new();
    private TypeDefinition isUnmanagedAttributeType;

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

    public IEnumerable<TypeRewriteContext> Types => myNewTypeMap.Values;
    public IEnumerable<TypeRewriteContext> OriginalTypes => myOldTypeMap.Values;

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

    public void RegisterTypeContext(TypeRewriteContext context)
    {
        myNewTypeMap.Add(context.NewType, context);
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

    public TypeReference RewriteTypeRef(TypeReference? typeRef, bool typeIsBoxed)
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

            var convertedElementType = RewriteTypeRef(elementType, typeIsBoxed);
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
                return RewriteTypeRef(genericParameterDeclaringType, typeIsBoxed).GenericParameters[genericParameter.Position];

            return RewriteMethodRef(genericParameter.DeclaringMethod).GenericParameters[genericParameter.Position];
        }

        if (typeRef is ByReferenceType byRef)
            return new ByReferenceType(RewriteTypeRef(byRef.ElementType, typeIsBoxed));

        if (typeRef is PointerType pointerType)
            return new PointerType(RewriteTypeRef(pointerType.ElementType, typeIsBoxed));

        if (typeRef is GenericInstanceType genericInstance)
        {
            var genericTypeContext = GetTypeContext(genericInstance);
            if (genericTypeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct && !IsUnmanaged(typeRef, typeIsBoxed))
            {
                var newRef = new GenericInstanceType(sourceModule.ImportReference(genericTypeContext.BoxedTypeContext.NewType));
                foreach (var originalParameter in genericInstance.GenericArguments)
                    newRef.GenericArguments.Add(RewriteTypeRef(originalParameter, typeIsBoxed));

                return newRef;
            }
            else
            {
                var newRef = new GenericInstanceType(RewriteTypeRef(genericInstance.ElementType, typeIsBoxed));
                foreach (var originalParameter in genericInstance.GenericArguments)
                    newRef.GenericArguments.Add(RewriteTypeRef(originalParameter, typeIsBoxed));

                return newRef;
            }
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

        var target = GetTypeContext(typeRef);

        if (typeIsBoxed && target.BoxedTypeContext != null)
        {
            target = target.BoxedTypeContext;
        }

        return sourceModule.ImportReference(target.NewType);
    }

    private TypeRewriteContext GetTypeContext(TypeReference typeRef)
    {
        return GlobalContext.GetNewTypeForOriginal(typeRef.Resolve());
    }

    private bool IsUnmanaged(TypeReference originalType, bool typeIsBoxed)
    {
        if (originalType is GenericParameter genericParameter)
        {
            GenericParameter newGenericParameter = (GenericParameter)RewriteTypeRef(genericParameter, typeIsBoxed);
            return newGenericParameter.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Equals("IsUnmanagedAttribute"));
        }

        if (originalType is GenericInstanceType genericInstanceType)
        {
            foreach (TypeReference genericArgument in genericInstanceType.GenericArguments)
            {
                if (!IsUnmanaged(genericArgument, typeIsBoxed))
                    return false;
            }
        }

        var paramTypeContext = GetTypeContext(originalType);
        return paramTypeContext.ComputedTypeSpecifics.IsBlittable();
    }

    public TypeDefinition GetOrInjectIsUnmanagedAttribute()
    {
        if (isUnmanagedAttributeType != null)
            return isUnmanagedAttributeType;

        isUnmanagedAttributeType = new TypeDefinition("System.Runtime.CompilerServices", "IsUnmanagedAttribute", TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed, NewAssembly.MainModule.ImportReference(typeof(Attribute)));
        NewAssembly.MainModule.Types.Add(isUnmanagedAttributeType);

        var attributeCctr = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName, NewAssembly.MainModule.TypeSystem.Void);
        isUnmanagedAttributeType.Methods.Add(attributeCctr);
        var ilProcessor = attributeCctr.Body.GetILProcessor();
        ilProcessor.Emit(OpCodes.Ldarg_0);
        ilProcessor.Emit(OpCodes.Call, NewAssembly.MainModule.ImportReference(isUnmanagedAttributeType.BaseType.DefaultCtorFor()));
        ilProcessor.Emit(OpCodes.Ret);
        return isUnmanagedAttributeType;
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
