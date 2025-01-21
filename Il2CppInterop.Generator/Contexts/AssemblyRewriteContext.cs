using System.Diagnostics;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
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
    private TypeDefinition? isUnmanagedAttributeType;

    private readonly Dictionary<TypeDefinition, TypeRewriteContext> myOldTypeMap = new();
    public readonly AssemblyDefinition NewAssembly;

    public readonly AssemblyDefinition OriginalAssembly;

    public AssemblyRewriteContext(RewriteGlobalContext globalContext, AssemblyDefinition originalAssembly,
        AssemblyDefinition newAssembly)
    {
        OriginalAssembly = originalAssembly;
        NewAssembly = newAssembly;
        GlobalContext = globalContext;

        Imports = globalContext.ImportsMap.GetOrCreate(newAssembly.ManifestModule!,
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

    public IMethodDefOrRef RewriteMethodRef(IMethodDefOrRef methodRef)
    {
        var newType = GlobalContext.GetNewTypeForOriginal(methodRef.DeclaringType!.Resolve()!);
        var newMethod = newType.GetMethodByOldMethod(methodRef.Resolve()!).NewMethod;
        return NewAssembly.ManifestModule!.DefaultImporter.ImportMethod(newMethod);
    }

    public ITypeDefOrRef RewriteTypeRef(ITypeDescriptor typeRef, GenericParameterContext context = default, bool typeIsBoxed = false)
    {
        return RewriteTypeRef(typeRef?.ToTypeSignature(), context, typeIsBoxed).ToTypeDefOrRef();
    }

    public TypeSignature RewriteTypeRef(TypeSignature? typeRef, GenericParameterContext context = default, bool typeIsBoxed = false)
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

            var convertedElementType = RewriteTypeRef(elementType, context, typeIsBoxed);
            if (elementType is GenericParameterSignature)
                return new GenericInstanceTypeSignature(Imports.Il2CppArrayBase.ToTypeDefOrRef(), false, convertedElementType);

            return new GenericInstanceTypeSignature(convertedElementType.IsValueType
                    ? Imports.Il2CppStructArray.ToTypeDefOrRef()
                    : Imports.Il2CppReferenceArray.ToTypeDefOrRef(), false, convertedElementType);
        }

        if (typeRef is GenericParameterSignature genericParameter)
            return new GenericParameterSignature(sourceModule, genericParameter.ParameterType, genericParameter.Index);

        if (typeRef is ByReferenceTypeSignature byRef)
            return new ByReferenceTypeSignature(RewriteTypeRef(byRef.BaseType, context, typeIsBoxed));

        if (typeRef is PointerTypeSignature pointerType)
            return new PointerTypeSignature(RewriteTypeRef(pointerType.BaseType, context, typeIsBoxed));

        if (typeRef is GenericInstanceTypeSignature genericInstance)
        {
            var genericTypeContext = GetTypeContext(genericInstance);
            if (genericTypeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct && !IsUnmanaged(typeRef, context))
            {
                var type = sourceModule.DefaultImporter.ImportType(genericTypeContext.BoxedTypeContext.NewType);
                var newRef = new GenericInstanceTypeSignature(type, type.IsValueType);
                foreach (var originalParameter in genericInstance.TypeArguments)
                    newRef.TypeArguments.Add(RewriteTypeRef(originalParameter, context, typeIsBoxed));

                return newRef;
            }
            else
            {
                var genericType = RewriteTypeRef(genericInstance.GenericType.ToTypeSignature(), context, typeIsBoxed).ToTypeDefOrRef();
                var newRef = new GenericInstanceTypeSignature(genericType, genericType.IsValueType);
                foreach (var originalParameter in genericInstance.TypeArguments)
                    newRef.TypeArguments.Add(RewriteTypeRef(originalParameter, context, typeIsBoxed));

                return newRef;
            }
        }

        if (typeRef.IsPrimitive() || typeRef.FullName == "System.TypedReference")
            return sourceModule.ImportCorlibReference(typeRef.FullName);

        if (typeRef.FullName == "System.Void")
            return Imports.Module.Void();

        if (typeRef.FullName == "System.String")
            return Imports.Module.String();

        if (typeRef.FullName == "System.Object")
            return sourceModule.DefaultImporter.ImportType(GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Object").NewType).ToTypeSignature();

        if (typeRef.FullName == "System.Attribute")
            return sourceModule.DefaultImporter.ImportType(GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Attribute").NewType).ToTypeSignature();

        var target = GetTypeContext(typeRef);

        if (typeIsBoxed && target.BoxedTypeContext != null)
        {
            target = target.BoxedTypeContext;
        }

        return sourceModule.DefaultImporter.ImportType(target.NewType).ToTypeSignature();
    }

    private TypeRewriteContext GetTypeContext(TypeSignature typeRef)
    {
        return GlobalContext.GetNewTypeForOriginal(typeRef.Resolve()!);
    }

    private bool IsUnmanaged(TypeSignature originalType, GenericParameterContext context)
    {
        if (originalType is GenericParameterSignature parameterSignature)
        {
            var genericParameter = context.GetGenericParameter(parameterSignature)!;
            return genericParameter.IsUnmanaged();
        }

        if (originalType is GenericInstanceTypeSignature genericInstanceType)
        {
            foreach (TypeSignature genericArgument in genericInstanceType.TypeArguments)
            {
                if (!IsUnmanaged(genericArgument, context))
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

        var importer = NewAssembly.ManifestModule!.DefaultImporter;

        isUnmanagedAttributeType = new TypeDefinition(
            "System.Runtime.CompilerServices",
            "IsUnmanagedAttribute",
            TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed,
            importer.ImportType(typeof(Attribute)));

        NewAssembly.ManifestModule!.TopLevelTypes.Add(isUnmanagedAttributeType);

        var attributeCctr = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RuntimeSpecialName | MethodAttributes.SpecialName,
            MethodSignature.CreateInstance(NewAssembly.ManifestModule.Void()));
        attributeCctr.CilMethodBody = new CilMethodBody(attributeCctr);

        isUnmanagedAttributeType.Methods.Add(attributeCctr);
        var ilProcessor = attributeCctr.CilMethodBody!.Instructions;
        ilProcessor.Add(OpCodes.Ldarg_0);

        var method = new MemberReference(
            isUnmanagedAttributeType.BaseType!,
            ".ctor",
            MethodSignature.CreateInstance(NewAssembly.ManifestModule.Void()));

        ilProcessor.Add(OpCodes.Call, importer.ImportMethod(method));
        ilProcessor.Add(OpCodes.Ret);

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

    private string GetDebuggerDisplay()
    {
        return NewAssembly.FullName;
    }
}
