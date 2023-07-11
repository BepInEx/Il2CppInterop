// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Generator.Contexts;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Il2CppInterop.Generator.Utils;

public class RuntimeAssemblyReferences
{
    private readonly Dictionary<string, TypeReference> allTypes = new();
    private readonly RewriteGlobalContext globalCtx;

    public RuntimeAssemblyReferences(ModuleDefinition module, RewriteGlobalContext globalContext)
    {
        Module = module;
        globalCtx = globalContext;
        InitTypeRefs();
        InitMethodRefs();
    }

    public ModuleDefinition Module { get; }

    public Memoize<TypeReference, MethodReference> Il2CppRefrenceArrayctor { get; private set; }
    public Lazy<MethodReference> Il2CppStringArrayctor { get; private set; }
    public Memoize<TypeReference, MethodReference> Il2CppStructArrayctor { get; private set; }
    public Memoize<TypeReference, MethodReference> Il2CppRefrenceArrayctor_size { get; private set; }
    public Lazy<MethodReference> Il2CppStringArrayctor_size { get; private set; }
    public Memoize<TypeReference, MethodReference> Il2CppStructArrayctor_size { get; private set; }
    public Lazy<MethodReference> IL2CPP_Il2CppObjectBaseToPtr { get; private set; }
    public Lazy<MethodReference> IL2CPP_Il2CppObjectBaseToPtrNotNull { get; private set; }
    public Lazy<MethodReference> IL2CPP_Il2CppStringToManaged { get; private set; }
    public Lazy<MethodReference> IL2CPP_ManagedStringToIl2Cpp { get; private set; }
    public Lazy<MethodReference> Il2CppObjectBase_Cast { get; private set; }
    public Lazy<MethodReference> Il2CppObjectBase_TryCast { get; private set; }
    public Lazy<MethodReference> Il2CppObjectPool_Get { get; private set; }
    public Lazy<MethodReference> IL2CPP_ResolveICall { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_gc_wbarrier_set_field { get; private set; }
    public Lazy<MethodReference> IL2CPP_FieldWriteWbarrierStub { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_field_get_offset { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_field_static_get_value { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_field_static_set_value { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_runtime_invoke { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_runtime_class_init { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_object_unbox { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_value_box { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_class_value_size { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_object_get_class { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_class_is_valuetype { get; private set; }
    public Lazy<MethodReference> Il2CppException_RaiseExceptionIfNecessary { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_object_get_virtual_method { get; private set; }
    public Lazy<MethodReference> IL2CPP_GetIl2CppField { get; private set; }
    public Lazy<MethodReference> IL2CPP_GetIl2CppNestedType { get; private set; }
    public Lazy<MethodReference> IL2CPP_GetIl2CppClass { get; private set; }
    public Lazy<MethodReference> IL2CPP_GetIl2CppMethod { get; private set; }
    public Lazy<MethodReference> IL2CPP_GetIl2CppMethodByToken { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_class_get_type { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_class_from_type { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_object_new { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_method_get_from_reflection { get; private set; }
    public Lazy<MethodReference> IL2CPP_il2cpp_method_get_object { get; private set; }
    public Lazy<MethodReference> IL2CPP_PointerToValueGeneric { get; private set; }
    public Lazy<MethodReference> IL2CPP_RenderTypeName { get; private set; }
    public Lazy<MethodReference> OriginalNameAttributector { get; private set; }
    public Lazy<MethodReference> ObfuscatedNameAttributector { get; private set; }
    public Lazy<MethodReference> CallerCountAttributector { get; private set; }
    public Lazy<MethodReference> CachedScanResultsAttributector { get; private set; }
    public Lazy<MethodReference> Il2CppSystemDelegateCombine { get; private set; }
    public Lazy<MethodReference> Il2CppSystemDelegateRemove { get; private set; }
    public Lazy<MethodReference> Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle { get; private set; }

    public MethodReference WriteFieldWBarrier => globalCtx.HasGcWbarrierFieldWrite
        ? IL2CPP_il2cpp_gc_wbarrier_set_field.Value
        : IL2CPP_FieldWriteWbarrierStub.Value;

    public TypeReference Il2CppObjectBase { get; private set; }
    public TypeReference Il2CppObjectPool { get; private set; }
    public TypeReference Il2CppStringArray { get; private set; }
    public TypeReference Il2CppArrayBase { get; private set; }
    public TypeReference Il2CppStructArray { get; private set; }
    public TypeReference Il2CppReferenceArray { get; private set; }
    public TypeReference Il2CppClassPointerStore { get; private set; }
    public TypeReference Il2Cpp { get; set; }
    public TypeReference RuntimeReflectionHelper { get; private set; }
    public TypeReference DelegateSupport { get; private set; }
    public TypeReference Il2CppException { get; private set; }

    public TypeReference NativeBoolean { get; private set; }

    private TypeReference ResolveType(string typeName)
    {
        return allTypes[typeName];
    }

    private void InitTypeRefs()
    {
        allTypes["System.Void"] = Module.ImportReference(typeof(void));
        allTypes["System.String[]"] = Module.ImportReference(typeof(string[]));
        allTypes["System.IntPtr"] = Module.ImportReference(typeof(IntPtr));
        allTypes["System.String"] = Module.ImportReference(typeof(string));
        allTypes["System.UInt32"] = Module.ImportReference(typeof(uint));
        allTypes["System.Void*"] = Module.ImportReference(typeof(void*));
        allTypes["System.Void**"] = Module.ImportReference(typeof(void**));
        allTypes["System.IntPtr&"] = Module.ImportReference(typeof(IntPtr).MakeByRefType());
        allTypes["System.Int32"] = Module.ImportReference(typeof(int));
        allTypes["System.UInt32&"] = Module.ImportReference(typeof(uint).MakeByRefType());
        allTypes["System.Boolean"] = Module.ImportReference(typeof(bool));
        allTypes["System.Int64"] = Module.ImportReference(typeof(long));

        var assemblyRef = new AssemblyNameReference("Il2CppInterop.Runtime", new Version(0, 0, 0, 0));
        Module.AssemblyReferences.Add(assemblyRef);

        Il2CppObjectBase =
            new TypeReference("Il2CppInterop.Runtime.InteropTypes", "Il2CppObjectBase", Module, assemblyRef);

        Il2CppObjectPool = new TypeReference("Il2CppInterop.Runtime.Runtime", "Il2CppObjectPool", Module, assemblyRef);

        Il2CppStringArray = new TypeReference("Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppStringArray", Module,
            assemblyRef);

        Il2CppArrayBase = new TypeReference("Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppArrayBase`1", Module,
            assemblyRef);
        Il2CppArrayBase.GenericParameters.Add(new GenericParameter("T", Il2CppArrayBase));

        Il2CppStructArray = new TypeReference("Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppStructArray`1",
            Module,
            assemblyRef);
        Il2CppStructArray.GenericParameters.Add(new GenericParameter("T", Il2CppStructArray));

        Il2CppReferenceArray = new TypeReference("Il2CppInterop.Runtime.InteropTypes.Arrays", "Il2CppReferenceArray`1",
            Module, assemblyRef);
        Il2CppReferenceArray.GenericParameters.Add(new GenericParameter("T", Il2CppReferenceArray));

        Il2CppClassPointerStore = new TypeReference("Il2CppInterop.Runtime", "Il2CppClassPointerStore`1",
            Module, assemblyRef);
        Il2CppClassPointerStore.GenericParameters.Add(new GenericParameter("T", Il2CppClassPointerStore));

        Il2Cpp = new TypeReference("Il2CppInterop.Runtime", "IL2CPP", Module, assemblyRef);

        RuntimeReflectionHelper =
            new TypeReference("Il2CppInterop.Runtime", "RuntimeReflectionHelper", Module, assemblyRef);

        DelegateSupport = new TypeReference("Il2CppInterop.Runtime", "DelegateSupport", Module, assemblyRef);

        Il2CppException = new TypeReference("Il2CppInterop.Runtime", "Il2CppException", Module, assemblyRef);

        NativeBoolean = new TypeReference("Il2CppInterop.Runtime", "NativeBoolean", Module, assemblyRef);
        NativeBoolean.IsValueType = true;

        allTypes["Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"] = Il2CppObjectBase;
        allTypes["Il2CppInterop.Runtime.Runtime.Il2CppObjectPool"] = Il2CppObjectPool;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray"] = Il2CppStringArray;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>"] = Il2CppReferenceArray;
        allTypes["Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<T>"] = Il2CppStructArray;
        allTypes["Il2CppInterop.Runtime.Il2CppException"] = Il2CppException;
        allTypes["Il2CppInterop.Runtime.IL2CPP"] = Il2Cpp;
        allTypes["Il2CppInterop.Runtime.NativeBoolean"] = NativeBoolean;
    }

    private void InitMethodRefs()
    {
        Il2CppRefrenceArrayctor = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>");
            var gp = owner.GenericParameters[0];
            var giOwner = new GenericInstanceType(owner);
            giOwner.GenericArguments.Add(param);
            var mr = new MethodReference(".ctor", ResolveType("System.Void"),
                giOwner)
            { HasThis = true };
            var paramType = new ArrayType(gp);
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, paramType));
            return mr;
        });

        Il2CppStringArrayctor = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference(".ctor", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray"));
            mr.HasThis = true;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String[]")));
            return mr;
        });

        Il2CppStructArrayctor = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<T>");
            var gp = owner.GenericParameters[0];
            var giOwner = new GenericInstanceType(owner);
            giOwner.GenericArguments.Add(param);
            var mr = new MethodReference(".ctor", ResolveType("System.Void"),
                giOwner);
            mr.HasThis = true;
            var paramType = new ArrayType(gp);
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, paramType));
            return mr;
        });

        Il2CppRefrenceArrayctor_size = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>");
            var giOwner = new GenericInstanceType(owner);
            giOwner.GenericArguments.Add(param);
            var mr = new MethodReference(".ctor", ResolveType("System.Void"),
                    giOwner)
            { HasThis = true };
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Int64")));
            return mr;
        });

        Il2CppStringArrayctor_size = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference(".ctor", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray"));
            mr.HasThis = true;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Int64")));
            return mr;
        });

        Il2CppStructArrayctor_size = new((param) =>
        {
            var owner = ResolveType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<T>");
            var giOwner = new GenericInstanceType(owner);
            giOwner.GenericArguments.Add(param);
            var mr = new MethodReference(".ctor", ResolveType("System.Void"),
                giOwner);
            mr.HasThis = true;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Int64")));
            return mr;
        });

        IL2CPP_Il2CppObjectBaseToPtr = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("Il2CppObjectBaseToPtr", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None,
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase")));
            return mr;
        });

        IL2CPP_Il2CppObjectBaseToPtrNotNull = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("Il2CppObjectBaseToPtrNotNull", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None,
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase")));
            return mr;
        });

        IL2CPP_Il2CppStringToManaged = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("Il2CppStringToManaged", ResolveType("System.String"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_ManagedStringToIl2Cpp = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("ManagedStringToIl2Cpp", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            return mr;
        });

        Il2CppObjectBase_Cast = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("Cast", Module.Void(),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"));
            var gp0 = new GenericParameter("T", mr);
            mr.GenericParameters.Add(gp0);
            mr.ReturnType = gp0;
            mr.HasThis = true;
            return mr;
        });

        Il2CppObjectBase_TryCast = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("TryCast", Module.Void(),
                ResolveType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"));
            var gp0 = new GenericParameter("T", mr);
            mr.GenericParameters.Add(gp0);
            mr.ReturnType = gp0;
            mr.HasThis = true;
            return mr;
        });

        Il2CppObjectPool_Get = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("Get", Module.Void(),
                ResolveType("Il2CppInterop.Runtime.Runtime.Il2CppObjectPool"));
            var gp0 = new GenericParameter("T", mr);
            mr.GenericParameters.Add(gp0);
            mr.ReturnType = gp0;
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("ptr", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_ResolveICall = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("ResolveICall", Module.Void(),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            var gp0 = new GenericParameter("T", mr);
            mr.GenericParameters.Add(gp0);
            mr.ReturnType = gp0;
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            return mr;
        });

        IL2CPP_il2cpp_gc_wbarrier_set_field = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_gc_wbarrier_set_field", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_FieldWriteWbarrierStub = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("FieldWriteWbarrierStub", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_field_get_offset = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_field_get_offset", ResolveType("System.UInt32"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_field_static_get_value = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_field_static_get_value", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Void*")));
            return mr;
        });

        IL2CPP_il2cpp_field_static_set_value = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_field_static_set_value", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Void*")));
            return mr;
        });

        IL2CPP_il2cpp_runtime_invoke = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_runtime_invoke", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Void**")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr&")));
            return mr;
        });

        IL2CPP_il2cpp_runtime_class_init = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_runtime_class_init", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_object_unbox = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_object_unbox", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_value_box = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_value_box", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_class_value_size = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_class_value_size", ResolveType("System.Int32"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.UInt32&")));
            return mr;
        });

        IL2CPP_il2cpp_object_get_class = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_object_get_class", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_class_is_valuetype = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_class_is_valuetype", ResolveType("System.Boolean"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        Il2CppException_RaiseExceptionIfNecessary = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("RaiseExceptionIfNecessary", ResolveType("System.Void"),
                ResolveType("Il2CppInterop.Runtime.Il2CppException"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_object_get_virtual_method = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_object_get_virtual_method", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_GetIl2CppField = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("GetIl2CppField", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            return mr;
        });

        IL2CPP_GetIl2CppNestedType = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("GetIl2CppNestedType", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            return mr;
        });

        IL2CPP_GetIl2CppClass = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("GetIl2CppClass", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            return mr;
        });

        IL2CPP_GetIl2CppMethod = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("GetIl2CppMethod", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Boolean")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.String[]")));
            return mr;
        });

        IL2CPP_GetIl2CppMethodByToken = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("GetIl2CppMethodByToken", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Int32")));
            return mr;
        });

        IL2CPP_il2cpp_class_get_type = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_class_get_type", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_class_from_type = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_class_from_type", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_object_new = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_object_new", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_method_get_from_reflection = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_method_get_from_reflection", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_il2cpp_method_get_object = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("il2cpp_method_get_object", ResolveType("System.IntPtr"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            return mr;
        });

        IL2CPP_PointerToValueGeneric = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("PointerToValueGeneric", Module.Void(),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            var gp0 = new GenericParameter("T", mr);
            mr.GenericParameters.Add(gp0);
            mr.ReturnType = gp0;
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.IntPtr")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Boolean")));
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Boolean")));
            return mr;
        });

        IL2CPP_RenderTypeName = new Lazy<MethodReference>(() =>
        {
            var mr = new MethodReference("RenderTypeName", ResolveType("System.String"),
                ResolveType("Il2CppInterop.Runtime.IL2CPP"));
            var gp0 = new GenericParameter("T", mr);
            mr.GenericParameters.Add(gp0);
            mr.HasThis = false;
            mr.Parameters.Add(new ParameterDefinition("", ParameterAttributes.None, ResolveType("System.Boolean")));
            return mr;
        });

        OriginalNameAttributector = new Lazy<MethodReference>(() => new MethodReference(".ctor",
                Module.Void(),
                Module.ImportReference(typeof(OriginalNameAttribute)))
        {
            HasThis = true,
            Parameters = {
                new ParameterDefinition(Module.String()),
                new ParameterDefinition(Module.String()),
                new ParameterDefinition(Module.String())
            }
        });

        ObfuscatedNameAttributector = new Lazy<MethodReference>(() => new MethodReference(".ctor",
                Module.Void(),
                Module.ImportReference(typeof(ObfuscatedNameAttribute)))
        { HasThis = true, Parameters = { new ParameterDefinition(Module.String()) } });

        CallerCountAttributector = new Lazy<MethodReference>(() =>
            new MethodReference(".ctor", Module.Void(), Module.ImportReference(typeof(CallerCountAttribute)))
            { HasThis = true, Parameters = { new ParameterDefinition(Module.Int()) } });

        CachedScanResultsAttributector = new Lazy<MethodReference>(() =>
            new MethodReference(".ctor", Module.Void(),
                Module.ImportReference(typeof(CachedScanResultsAttribute)))
            {
                HasThis = true
            });

        Il2CppSystemDelegateCombine = new Lazy<MethodReference>(() =>
            Module.ImportReference(globalCtx.GetAssemblyByName("mscorlib").NewAssembly.MainModule
                .GetType("Il2CppSystem.Delegate").Methods.Single(m => m.Name == "Combine" && m.Parameters.Count == 2)));

        Il2CppSystemDelegateRemove = new Lazy<MethodReference>(() =>
            Module.ImportReference(globalCtx.GetAssemblyByName("mscorlib").NewAssembly.MainModule
                .GetType("Il2CppSystem.Delegate").Methods.Single(m => m.Name == "Remove")));

        Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle = new Lazy<MethodReference>(() =>
        {
            var declaringTypeRef = RuntimeReflectionHelper;
            var returnTypeRef = Module.ImportReference(globalCtx.GetAssemblyByName("mscorlib").NewAssembly.MainModule
                .GetType("Il2CppSystem.RuntimeTypeHandle"));
            var methodReference = new MethodReference("GetRuntimeTypeHandle", returnTypeRef, declaringTypeRef)
            { HasThis = false };
            methodReference.GenericParameters.Add(new GenericParameter("T", methodReference));
            return Module.ImportReference(methodReference);
        });
    }
}
