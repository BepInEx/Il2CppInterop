// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mono.Cecil;

var ass = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("MyAssembly", new Version(1, 0)),
    "MyAssembly.dll", ModuleKind.Dll);

var Module = ass.MainModule;

var myIl2CppClassPointerStoreReference = Module.ImportReference(typeof(Il2CppClassPointerStore<>));
var myIl2CppReferenceArray = Module.ImportReference(typeof(Il2CppReferenceArray<>));
var myIl2CppStructArray = Module.ImportReference(typeof(Il2CppStructArray<>));
var myIl2CppStringArray = Module.ImportReference(typeof(Il2CppStringArray));

static ConstructorInfo FindArrayConstructor(Type type)
{
    return type.GetConstructors().Single(constructorInfo =>
    {
        var parameters = constructorInfo.GetParameters();
        return parameters.Length == 1 && parameters.Single().ParameterType.IsArray;
    });
}

var myIl2CppReferenceArrayCtor = FindArrayConstructor(typeof(Il2CppReferenceArray<>));
var r = Module.ImportReference(myIl2CppReferenceArrayCtor);
Console.WriteLine(r);
var myIl2CppStructArrayCtor = FindArrayConstructor(typeof(Il2CppStructArray<>));
r = Module.ImportReference(myIl2CppStructArrayCtor);
Console.WriteLine(r);
var myIl2CppStringArrayCtor = Module.ImportReference(FindArrayConstructor(typeof(Il2CppStringArray)));
var myIl2CppArrayBase = Module.ImportReference(typeof(Il2CppArrayBase<>));
var myIl2CppArrayBaseSetlfSubst = Module.ImportReference(new GenericInstanceType(myIl2CppArrayBase)
{ GenericArguments = { myIl2CppArrayBase.GenericParameters[0] } });
var myIl2CppObjectBaseReference = Module.ImportReference(typeof(Il2CppObjectBase));
var myIl2CppObjectToPointer = Module.ImportReference(typeof(IL2CPP).GetMethod("Il2CppObjectBaseToPtr"));
var myIl2CppObjectToPointerNotNull = Module.ImportReference(typeof(IL2CPP).GetMethod("Il2CppObjectBaseToPtrNotNull"));
var myStringFromNative = Module.ImportReference(typeof(IL2CPP).GetMethod("Il2CppStringToManaged"));
var myStringToNative = Module.ImportReference(typeof(IL2CPP).GetMethod("ManagedStringToIl2Cpp"));
var myIl2CppObjectCast = Module.ImportReference(typeof(Il2CppObjectBase).GetMethod("Cast"));
var myIl2CppObjectTryCast = Module.ImportReference(typeof(Il2CppObjectBase).GetMethod("TryCast"));
var myIl2CppResolveICall = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.ResolveICall)));
var myWriteFieldWBarrier1 =
    Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_gc_wbarrier_set_field)));
var myWriteFieldWBarrier2 = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.FieldWriteWbarrierStub)));
var myFieldGetOffset = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_field_get_offset"));
var myFieldStaticGet = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_field_static_get_value"));
var myFieldStaticSet = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_field_static_set_value"));
var myRuntimeInvoke = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_runtime_invoke"));
var myRuntimeClassInit = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_runtime_class_init"));
var myObjectUnbox = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_object_unbox"));
var myObjectBox = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_value_box)));
var myValueSizeGet = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_class_value_size)));
var myObjectGetClass = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_object_get_class)));
var myClassIsValueType = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_class_is_valuetype)));
var myRaiseExceptionIfNecessary =
    Module.ImportReference(typeof(Il2CppException).GetMethod("RaiseExceptionIfNecessary"));
var myGetVirtualMethod = Module.ImportReference(typeof(IL2CPP).GetMethod("il2cpp_object_get_virtual_method"));
var myGetFieldPtr = Module.ImportReference(typeof(IL2CPP).GetMethod("GetIl2CppField"));
var myGetIl2CppNestedClass = Module.ImportReference(typeof(IL2CPP).GetMethod("GetIl2CppNestedType"));
var myGetIl2CppGlobalClass = Module.ImportReference(typeof(IL2CPP).GetMethod("GetIl2CppClass"));
var myGetIl2CppMethod = Module.ImportReference(typeof(IL2CPP).GetMethod("GetIl2CppMethod"));
var myGetIl2CppMethodFromToken =
    Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.GetIl2CppMethodByToken)));
var myGetIl2CppTypeFromClass = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_class_get_type)));
var myGetIl2CppTypeToClass = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_class_from_type)));
var myIl2CppNewObject = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_object_new)));
var myIl2CppMethodInfoFromReflection =
    Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_method_get_from_reflection)));
var myIl2CppMethodInfoToReflection =
    Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_method_get_object)));
var myIl2CppPointerToGeneric = Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.PointerToValueGeneric)));
var myIl2CppRenderTypeNameGeneric =
    Module.ImportReference(typeof(IL2CPP).GetMethod(nameof(IL2CPP.RenderTypeName), new[] { typeof(bool) }));

// var myDelegateCombine =
//     Module.ImportReference(myContext.GetAssemblyByName("mscorlib").NewAssembly.MainModule.GetType("Il2CppSystem.Delegate").Methods.Single(m => m.Name == "Combine" && m.Parameters.Count == 2));
// var myDelegateRemove = new Lazy<MethodReference>(() =>
//     Module.ImportReference(myContext.GetAssemblyByName("mscorlib").NewAssembly.MainModule.GetType("Il2CppSystem.Delegate").Methods.Single(m => m.Name == "Remove")));

// var myLdTokUnstrippedImpl = new Lazy<MethodReference>(() =>
// {
//     var declaringTypeRef = Module.ImportReference(typeof(RuntimeReflectionHelper));
//     var returnTypeRef = Module.ImportReference(myContext.GetAssemblyByName("mscorlib").NewAssembly.MainModule.GetType("Il2CppSystem.RuntimeTypeHandle"));
//     var methodReference = new MethodReference("GetRuntimeTypeHandle", returnTypeRef, declaringTypeRef) { HasThis = false };
//     methodReference.GenericParameters.Add(new GenericParameter("T", methodReference));
//     return Module.ImportReference(methodReference);
// });

var myObfuscatedNameAttributeCtor = new MethodReference(".ctor", Module.TypeSystem.Void,
        Module.ImportReference(typeof(ObfuscatedNameAttribute)))
{ HasThis = true, Parameters = { new ParameterDefinition(Module.TypeSystem.String) } };

var myCallerCountAttributeCtor =
    new MethodReference(".ctor", Module.TypeSystem.Void, Module.ImportReference(typeof(CallerCountAttribute)))
    { HasThis = true, Parameters = { new ParameterDefinition(Module.TypeSystem.Int32) } };

var myCachedScanResultsAttributeCtor = new MethodReference(".ctor", Module.TypeSystem.Void,
    Module.ImportReference(typeof(CachedScanResultsAttribute)))
{
    HasThis = true
};

static List<T> FlagsEnumToArray<T>(T flags) where T : struct, Enum
{
    var values = new List<T>();
    var flagsInt = Convert.ToInt32(flags);
    foreach (T value in Enum.GetValues(typeof(T)))
    {
        var valueInt = Convert.ToInt32(value);
        if ((flagsInt & valueInt) == valueInt)
            values.Add(value);
    }

    return values;
}

var types = new HashSet<string>();
var methodReferences = new HashSet<string>();

void PrintMethodReference(MethodReference mr)
{
    //var mm = new MethodReference("", null, null);
    var sb = new StringBuilder();
    sb.AppendLine($"{mr.DeclaringType.Name}_{mr.Name} = new(() => {{");
    methodReferences.Add($"{mr.DeclaringType.Name}_{mr.Name}");
    var returnType = mr.ReturnType.IsGenericParameter
        ? "module.TypeSystem.Void"
        : $@"ResolveType(""{mr.ReturnType.FullName}"")";
    if (!mr.ReturnType.IsGenericParameter)
        types.Add(mr.ReturnType.FullName);
    types.Add(mr.DeclaringType.FullName);
    sb.AppendLine(
        $@"var mr = new MethodReference(""{mr.Name}"", {returnType}, ResolveType(""{mr.DeclaringType.FullName}""));");
    var gParamDict = new Dictionary<string, string>();
    if (mr.HasGenericParameters)
        for (var i = 0; i < mr.GenericParameters.Count; i++)
        {
            var gp = mr.GenericParameters[i];
            sb.AppendLine($@"var gp{i} = new GenericParameter(""{gp.Name}"", mr);");
            sb.AppendLine($@"mr.GenericParameters.Add(gp{i});");
            gParamDict.Add(gp.Name, $"gp{i}");
        }

    if (mr.ReturnType.IsGenericParameter) sb.AppendLine($@"mr.ReturnType = {gParamDict[mr.ReturnType.Name]};");
    sb.AppendLine($@"mr.HasThis = {mr.HasThis.ToString().ToLower()};");
    foreach (var p in mr.Parameters)
    {
        var paramType = p.ParameterType.IsGenericParameter
            ? $"{gParamDict[p.ParameterType.Name]}"
            : $@"ResolveType(""{p.ParameterType.FullName}"")";
        if (!p.ParameterType.IsGenericParameter)
            types.Add(p.ParameterType.FullName);
        sb.AppendLine(
            $@"mr.Parameters.Add(new ParameterDefinition(""{p.Name}"", {string.Join("|", FlagsEnumToArray(p.Attributes).Select(p => $"ParameterAttributes.{p}"))}, {paramType}));");
    }

    sb.AppendLine(@"return mr;");
    sb.AppendLine("});");
    Console.WriteLine(sb.ToString());
}

PrintMethodReference(myIl2CppStringArrayCtor);
PrintMethodReference(myIl2CppObjectToPointer);
PrintMethodReference(myIl2CppObjectToPointerNotNull);
PrintMethodReference(myStringFromNative);
PrintMethodReference(myStringToNative);
PrintMethodReference(myIl2CppObjectCast);
PrintMethodReference(myIl2CppObjectTryCast);
PrintMethodReference(myIl2CppResolveICall);
PrintMethodReference(myWriteFieldWBarrier1);
PrintMethodReference(myWriteFieldWBarrier2);
PrintMethodReference(myFieldGetOffset);
PrintMethodReference(myFieldStaticGet);
PrintMethodReference(myFieldStaticSet);
PrintMethodReference(myRuntimeInvoke);
PrintMethodReference(myRuntimeClassInit);
PrintMethodReference(myObjectUnbox);
PrintMethodReference(myObjectBox);
PrintMethodReference(myValueSizeGet);
PrintMethodReference(myObjectGetClass);
PrintMethodReference(myClassIsValueType);
PrintMethodReference(myRaiseExceptionIfNecessary);
PrintMethodReference(myGetVirtualMethod);
PrintMethodReference(myGetFieldPtr);
PrintMethodReference(myGetIl2CppNestedClass);
PrintMethodReference(myGetIl2CppGlobalClass);
PrintMethodReference(myGetIl2CppMethod);
PrintMethodReference(myGetIl2CppMethodFromToken);
PrintMethodReference(myGetIl2CppTypeFromClass);
PrintMethodReference(myGetIl2CppTypeToClass);
PrintMethodReference(myIl2CppNewObject);
PrintMethodReference(myIl2CppMethodInfoFromReflection);
PrintMethodReference(myIl2CppMethodInfoToReflection);
PrintMethodReference(myIl2CppPointerToGeneric);
PrintMethodReference(myIl2CppRenderTypeNameGeneric);

foreach (var t in types) Console.WriteLine($@"allTypes[""{t}""] = null;");

foreach (var mrefs in methodReferences)
    Console.WriteLine($"public Lazy<MethodReference> {mrefs} {{get; private set;}}");