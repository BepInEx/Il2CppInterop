// ReSharper disable InconsistentNaming

using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Utils;

public class RuntimeAssemblyReferences
{
    private readonly Dictionary<string, TypeSignature> allTypes = new();
    private readonly RewriteGlobalContext globalCtx;

    public RuntimeAssemblyReferences(ModuleDefinition module, RewriteGlobalContext globalContext)
    {
        Module = module;
        globalCtx = globalContext;
        InitTypeRefs();
        InitMethodRefs();
    }

    public ModuleDefinition Module { get; }
#nullable disable
    public Memoize<TypeSignature, IMethodDefOrRef> Il2CppRefrenceArrayctor { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppStringArrayctor { get; private set; }
    public Memoize<TypeSignature, IMethodDefOrRef> Il2CppStructArrayctor { get; private set; }
    public Memoize<TypeSignature, IMethodDefOrRef> Il2CppRefrenceArrayctor_size { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppStringArrayctor_size { get; private set; }
    public Memoize<TypeSignature, IMethodDefOrRef> Il2CppStructArrayctor_size { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppArrayBase_get_Length { get; private set; }
    public Memoize<TypeSignature, IMethodDefOrRef> Il2CppArrayBase_get_Item { get; private set; }
    public Memoize<TypeSignature, IMethodDefOrRef> Il2CppArrayBase_set_Item { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_Il2CppObjectBaseToPtr { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_Il2CppObjectBaseToPtrNotNull { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_Il2CppStringToManaged { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_ManagedStringToIl2Cpp { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppObjectBase_Cast { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppObjectBase_TryCast { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppObjectPool_Get { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_ResolveICall { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_gc_wbarrier_set_field { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_FieldWriteWbarrierStub { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_field_get_offset { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_field_static_get_value { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_field_static_set_value { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_runtime_invoke { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_runtime_class_init { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_object_unbox { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_value_box { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_class_value_size { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_object_get_class { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_class_is_valuetype { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppException_RaiseExceptionIfNecessary { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_object_get_virtual_method { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_GetIl2CppField { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_GetIl2CppNestedType { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_GetIl2CppClass { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_GetIl2CppMethod { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_GetIl2CppMethodByToken { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_class_get_type { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_class_from_type { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_object_new { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_method_get_from_reflection { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_il2cpp_method_get_object { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_PointerToValueGeneric { get; private set; }
    public Lazy<IMethodDefOrRef> IL2CPP_RenderTypeName { get; private set; }
    public Lazy<IMethodDefOrRef> OriginalNameAttributector { get; private set; }
    public Lazy<IMethodDefOrRef> ObfuscatedNameAttributector { get; private set; }
    public Lazy<IMethodDefOrRef> CallerCountAttributector { get; private set; }
    public Lazy<IMethodDefOrRef> CachedScanResultsAttributector { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppSystemDelegateCombine { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppSystemDelegateRemove { get; private set; }
    public Lazy<IMethodDefOrRef> Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle { get; private set; }

    public IMethodDescriptor WriteFieldWBarrier => globalCtx.HasGcWbarrierFieldWrite
        ? IL2CPP_il2cpp_gc_wbarrier_set_field.Value
        : IL2CPP_FieldWriteWbarrierStub.Value;

    public TypeSignature Il2CppObjectBase { get; private set; }
    public TypeSignature Il2CppObjectPool { get; private set; }
    public TypeSignature Il2CppStringArray { get; private set; }
    public TypeSignature Il2CppArrayBase { get; private set; }
    public TypeSignature Il2CppStructArray { get; private set; }
    public TypeSignature Il2CppReferenceArray { get; private set; }
    public TypeSignature Il2CppClassPointerStore { get; private set; }
    public TypeSignature Il2Cpp { get; set; }
    public TypeSignature RuntimeReflectionHelper { get; private set; }
    public TypeSignature DelegateSupport { get; private set; }
    public TypeSignature Il2CppException { get; private set; }
#nullable enable
    private TypeSignature ResolveType(string typeName)
    {
        return allTypes[typeName];
    }

    private void InitTypeRefs()
    {
        allTypes["System.Void"] = Module.DefaultImporter.ImportTypeSignature(typeof(void));
        allTypes["System.String[]"] = Module.DefaultImporter.ImportTypeSignature(typeof(string[]));
        allTypes["System.IntPtr"] = Module.DefaultImporter.ImportTypeSignature(typeof(IntPtr));
        allTypes["System.String"] = Module.DefaultImporter.ImportTypeSignature(typeof(string));
        allTypes["System.UInt32"] = Module.DefaultImporter.ImportTypeSignature(typeof(uint));
        allTypes["System.Void*"] = Module.DefaultImporter.ImportTypeSignature(typeof(void*));
        allTypes["System.Void**"] = Module.DefaultImporter.ImportTypeSignature(typeof(void**));
        allTypes["System.IntPtr&"] = Module.DefaultImporter.ImportTypeSignature(typeof(IntPtr).MakeByRefType());
        allTypes["System.Int32"] = Module.DefaultImporter.ImportTypeSignature(typeof(int));
        allTypes["System.UInt32&"] = Module.DefaultImporter.ImportTypeSignature(typeof(uint).MakeByRefType());
        allTypes["System.Boolean"] = Module.DefaultImporter.ImportTypeSignature(typeof(bool));
        allTypes["System.Int64"] = Module.DefaultImporter.ImportTypeSignature(typeof(long));

        var assemblyRef = new AssemblyReference("Il2CppInterop.Runtime", new Version(0, 0, 0, 0));
        Module.AssemblyReferences.Add(assemblyRef);

        Il2CppObjectBase =
            new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes", "Il2CppObjectBase").ToTypeSignature();

        Il2CppObjectPool = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.Runtime", "Il2CppObjectPool").ToTypeSignature();

        Il2CppStringArray = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppStringArray").ToTypeSignature();

        Il2CppArrayBase = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppArrayBase`1").ToTypeSignature();

        var nonGenericIl2CppArrayBase = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppArrayBase").ToTypeSignature();

        var genericIl2CppArrayBase = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppArrayBase`1").ToTypeSignature();

        Il2CppStructArray = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppStructArray`1").ToTypeSignature();

        Il2CppReferenceArray = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppReferenceArray`1").ToTypeSignature();

        Il2CppClassPointerStore = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime", "Il2CppClassPointerStore`1").ToTypeSignature();

        Il2Cpp = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime", "IL2CPP").ToTypeSignature();

        RuntimeReflectionHelper =
            new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime", "RuntimeReflectionHelper").ToTypeSignature();

        DelegateSupport = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime", "DelegateSupport").ToTypeSignature();

        Il2CppException = new TypeReference(Module, assemblyRef, "Il2CppInterop.Runtime", "Il2CppException").ToTypeSignature();

        allTypes["Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"] = Il2CppObjectBase;
        allTypes["Il2CppInterop.Runtime.Runtime.Il2CppObjectPool"] = Il2CppObjectPool;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase"] = nonGenericIl2CppArrayBase;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<T>"] = genericIl2CppArrayBase;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray"] = Il2CppStringArray;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>"] = Il2CppReferenceArray;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<T>"] = Il2CppStructArray;
        allTypes["Il2CppInterop.Runtime.Il2CppException"] = Il2CppException;
        allTypes["Il2CppInterop.Runtime.IL2CPP"] = Il2Cpp;
    }

    private void InitMethodRefs()
    {
        Il2CppRefrenceArrayctor = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>");
            var gp = new GenericParameterSignature(GenericParameterType.Type, 0);
            var giOwner = owner.MakeGenericInstanceType(param).ToTypeDefOrRef();
            return ReferenceCreator.CreateInstanceMethodReference(".ctor", ResolveType("System.Void"),
                giOwner, gp.MakeSzArrayType());
        });

        Il2CppStringArrayctor = new Lazy<IMethodDefOrRef>(() =>
        {
            return ReferenceCreator.CreateInstanceMethodReference(".ctor", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray").ToTypeDefOrRef(), ResolveType("System.String[]"));
        });

        Il2CppStructArrayctor = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<T>");
            var gp = new GenericParameterSignature(GenericParameterType.Type, 0);
            var giOwner = new GenericInstanceTypeSignature(owner.ToTypeDefOrRef(), false);
            giOwner.TypeArguments.Add(param);
            var mr = ReferenceCreator.CreateInstanceMethodReference(".ctor", ResolveType("System.Void"),
                giOwner.ToTypeDefOrRef());
            var paramType = gp.MakeSzArrayType();
            ((MethodSignature)mr.Signature!).ParameterTypes.Add(paramType);
            return mr;
        });

        Il2CppRefrenceArrayctor_size = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>");
            var giOwner = new GenericInstanceTypeSignature(owner.ToTypeDefOrRef(), false);
            giOwner.TypeArguments.Add(param);
            var mr = ReferenceCreator.CreateInstanceMethodReference(".ctor", ResolveType("System.Void"),
                    giOwner.ToTypeDefOrRef(), ResolveType("System.Int64"));
            return mr;
        });

        Il2CppStringArrayctor_size = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateInstanceMethodReference(".ctor", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray").ToTypeDefOrRef(), ResolveType("System.Int64"));
            return mr;
        });

        Il2CppStructArrayctor_size = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<T>");
            var giOwner = owner.MakeGenericInstanceType(param).ToTypeDefOrRef();
            var mr = ReferenceCreator.CreateInstanceMethodReference(".ctor", ResolveType("System.Void"),
                giOwner, ResolveType("System.Int64"));
            return mr;
        });

        Il2CppArrayBase_get_Length = new Lazy<IMethodDefOrRef>(() =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase");
            var mr = ReferenceCreator.CreateInstanceMethodReference("get_Length", ResolveType("System.Int32"),
                owner.ToTypeDefOrRef());
            return mr;
        });

        Il2CppArrayBase_get_Item = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<T>");
            var giOwner = owner.MakeGenericInstanceType(param).ToTypeDefOrRef();
            var mr = ReferenceCreator.CreateInstanceMethodReference("get_Item", new GenericParameterSignature(Module, GenericParameterType.Type, 0),
                giOwner, ResolveType("System.Int32"));
            return mr;
        });

        Il2CppArrayBase_set_Item = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<T>");
            var giOwner = owner.MakeGenericInstanceType(param).ToTypeDefOrRef();
            var mr = ReferenceCreator.CreateInstanceMethodReference("set_Item", Module.Void(),
                giOwner, ResolveType("System.Int32"), new GenericParameterSignature(Module, GenericParameterType.Type, 0));
            return mr;
        });

        IL2CPP_Il2CppObjectBaseToPtr = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("Il2CppObjectBaseToPtr", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"));
            return mr;
        });

        IL2CPP_Il2CppObjectBaseToPtrNotNull = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("Il2CppObjectBaseToPtrNotNull", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"));
            return mr;
        });

        IL2CPP_Il2CppStringToManaged = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("Il2CppStringToManaged", ResolveType("System.String"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_ManagedStringToIl2Cpp = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("ManagedStringToIl2Cpp", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.String"));
            return mr;
        });

        Il2CppObjectBase_Cast = new Lazy<IMethodDefOrRef>(() =>
        {
            var gp0 = new GenericParameterSignature(GenericParameterType.Method, 0);
            var signature = MethodSignature.CreateInstance(gp0, 1);
            var mr = new MemberReference(ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase").ToTypeDefOrRef(), "Cast", signature);
            return mr;
        });

        Il2CppObjectBase_TryCast = new Lazy<IMethodDefOrRef>(() =>
        {
            var gp0 = new GenericParameterSignature(GenericParameterType.Method, 0);
            var signature = MethodSignature.CreateInstance(gp0, 1);
            var mr = new MemberReference(ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase").ToTypeDefOrRef(), "TryCast", signature);
            return mr;
        });

        Il2CppObjectPool_Get = new Lazy<IMethodDefOrRef>(() =>
        {
            var gp0 = new GenericParameterSignature(GenericParameterType.Method, 0);
            var signature = MethodSignature.CreateStatic(gp0, 1, ResolveType("System.IntPtr"));
            var mr = new MemberReference(ResolveType("Il2CppInterop.Runtime.Runtime.Il2CppObjectPool").ToTypeDefOrRef(), "Get", signature);
            return mr;
        });

        IL2CPP_ResolveICall = new Lazy<IMethodDefOrRef>(() =>
        {
            var gp0 = new GenericParameterSignature(GenericParameterType.Method, 0);
            var signature = MethodSignature.CreateStatic(gp0, 1, ResolveType("System.String"));
            var mr = new MemberReference(ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), "ResolveICall", signature);
            return mr;
        });

        IL2CPP_il2cpp_gc_wbarrier_set_field = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_gc_wbarrier_set_field", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_FieldWriteWbarrierStub = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("FieldWriteWbarrierStub", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_field_get_offset = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_field_get_offset", ResolveType("System.UInt32"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_field_static_get_value = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_field_static_get_value", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.Void*"));
            return mr;
        });

        IL2CPP_il2cpp_field_static_set_value = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_field_static_set_value", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.Void*"));
            return mr;
        });

        IL2CPP_il2cpp_runtime_invoke = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_runtime_invoke", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"), ResolveType("System.Void**"), ResolveType("System.IntPtr&"));
            return mr;
        });

        IL2CPP_il2cpp_runtime_class_init = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_runtime_class_init", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_object_unbox = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_object_unbox", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_value_box = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_value_box", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_class_value_size = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_class_value_size", ResolveType("System.Int32"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.UInt32&"));
            return mr;
        });

        IL2CPP_il2cpp_object_get_class = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_object_get_class", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_class_is_valuetype = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_class_is_valuetype", ResolveType("System.Boolean"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        Il2CppException_RaiseExceptionIfNecessary = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("RaiseExceptionIfNecessary", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.Il2CppException").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_object_get_virtual_method = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_object_get_virtual_method", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_GetIl2CppField = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("GetIl2CppField", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.String"));
            return mr;
        });

        IL2CPP_GetIl2CppNestedType = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("GetIl2CppNestedType", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.String"));
            return mr;
        });

        IL2CPP_GetIl2CppClass = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("GetIl2CppClass", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.String"), ResolveType("System.String"), ResolveType("System.String"));
            return mr;
        });

        IL2CPP_GetIl2CppMethod = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("GetIl2CppMethod", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(),
                ResolveType("System.IntPtr"), ResolveType("System.Boolean"), ResolveType("System.String"), ResolveType("System.String"), ResolveType("System.String[]"));
            return mr;
        });

        IL2CPP_GetIl2CppMethodByToken = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("GetIl2CppMethodByToken", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.Int32"));
            return mr;
        });

        IL2CPP_il2cpp_class_get_type = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_class_get_type", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_class_from_type = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_class_from_type", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_object_new = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_object_new", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_method_get_from_reflection = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_method_get_from_reflection", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_il2cpp_method_get_object = new Lazy<IMethodDefOrRef>(() =>
        {
            var mr = ReferenceCreator.CreateStaticMethodReference("il2cpp_method_get_object", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), ResolveType("System.IntPtr"), ResolveType("System.IntPtr"));
            return mr;
        });

        IL2CPP_PointerToValueGeneric = new Lazy<IMethodDefOrRef>(() =>
        {
            var gp0 = new GenericParameterSignature(GenericParameterType.Method, 0);
            var signature = MethodSignature.CreateStatic(gp0, 1, ResolveType("System.IntPtr"), ResolveType("System.Boolean"), ResolveType("System.Boolean"));
            var mr = new MemberReference(ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), "PointerToValueGeneric", signature);
            return mr;
        });

        IL2CPP_RenderTypeName = new Lazy<IMethodDefOrRef>(() =>
        {
            var gp0 = new GenericParameterSignature(GenericParameterType.Method, 0);
            var signature = MethodSignature.CreateStatic(ResolveType("System.String"), 1, ResolveType("System.Boolean"));
            var mr = new MemberReference(ResolveType("Il2CppInterop.Runtime.IL2CPP").ToTypeDefOrRef(), "RenderTypeName", signature);
            return mr;
        });

        OriginalNameAttributector = new Lazy<IMethodDefOrRef>(() => ReferenceCreator.CreateInstanceMethodReference(".ctor",
                Module.Void(),
                Module.DefaultImporter.ImportType(typeof(OriginalNameAttribute)), Module.String(), Module.String(), Module.String()));

        ObfuscatedNameAttributector = new Lazy<IMethodDefOrRef>(() => ReferenceCreator.CreateInstanceMethodReference(".ctor",
                Module.Void(),
                Module.DefaultImporter.ImportType(typeof(ObfuscatedNameAttribute)), Module.String()));

        CallerCountAttributector = new Lazy<IMethodDefOrRef>(() =>
            ReferenceCreator.CreateInstanceMethodReference(".ctor", Module.Void(), Module.DefaultImporter.ImportType(typeof(CallerCountAttribute)), Module.Int()));

        CachedScanResultsAttributector = new Lazy<IMethodDefOrRef>(() =>
            ReferenceCreator.CreateInstanceMethodReference(".ctor", Module.Void(),
                Module.DefaultImporter.ImportType(typeof(CachedScanResultsAttribute))));

        Il2CppSystemDelegateCombine = new Lazy<IMethodDefOrRef>(() =>
            Module.DefaultImporter.ImportMethod(globalCtx.GetAssemblyByName("mscorlib").NewAssembly.ManifestModule!
                .GetType("Il2CppSystem.Delegate").Methods.Single(m => m.Name == "Combine" && m.Parameters.Count == 2)));

        Il2CppSystemDelegateRemove = new Lazy<IMethodDefOrRef>(() =>
            Module.DefaultImporter.ImportMethod(globalCtx.GetAssemblyByName("mscorlib").NewAssembly.ManifestModule!
                .GetType("Il2CppSystem.Delegate").Methods.Single(m => m.Name == "Remove")));

        Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle = new Lazy<IMethodDefOrRef>(() =>
        {
            var declaringTypeRef = RuntimeReflectionHelper;
            var returnTypeRef = Module.DefaultImporter.ImportType(globalCtx.GetAssemblyByName("mscorlib").NewAssembly.ManifestModule!
                .GetType("Il2CppSystem.RuntimeTypeHandle"));
            var signature = MethodSignature.CreateStatic(returnTypeRef.ToTypeSignature(), 1);
            var methodReference = new MemberReference(declaringTypeRef.ToTypeDefOrRef(), "GetRuntimeTypeHandle", signature);
            return Module.DefaultImporter.ImportMethod(methodReference);
        });
    }
}
