using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

public static unsafe class IL2CPP
{
    private static readonly Dictionary<string, IntPtr> ourImagesMap = new();

    static IL2CPP()
    {
        var domain = il2cpp_domain_get();
        if (domain == IntPtr.Zero)
        {
            Logger.Instance.LogError("No il2cpp domain found; sad!");
            return;
        }

        uint assembliesCount = 0;
        var assemblies = il2cpp_domain_get_assemblies(domain, ref assembliesCount);
        for (var i = 0; i < assembliesCount; i++)
        {
            var image = il2cpp_assembly_get_image(assemblies[i]);
            var name = il2cpp_image_get_name_(image)!;
            ourImagesMap[name] = image;
        }
    }

    internal static IntPtr GetIl2CppImage(string name)
    {
        if (ourImagesMap.ContainsKey(name)) return ourImagesMap[name];
        return IntPtr.Zero;
    }

    internal static IntPtr[] GetIl2CppImages()
    {
        return ourImagesMap.Values.ToArray();
    }

    public static IntPtr GetIl2CppClass(string assemblyName, string namespaze, string className)
    {
        if (!ourImagesMap.TryGetValue(assemblyName, out var image))
        {
            Logger.Instance.LogError("Assembly {AssemblyName} is not registered in il2cpp", assemblyName);
            return IntPtr.Zero;
        }

        var clazz = il2cpp_class_from_name(image, namespaze, className);
        return clazz;
    }

    public static IntPtr GetIl2CppField(IntPtr clazz, string fieldName)
    {
        if (clazz == IntPtr.Zero) return IntPtr.Zero;

        var field = il2cpp_class_get_field_from_name(clazz, fieldName);
        if (field == IntPtr.Zero)
            Logger.Instance.LogError(
                "Field {FieldName} was not found on class {ClassName}", fieldName, il2cpp_class_get_name_(clazz));
        return field;
    }

    public static int GetIl2cppValueSize(IntPtr klass)
    {
        uint align = 0;
        return il2cpp_class_value_size(klass, ref align);
    }

    public static IntPtr GetIl2CppMethodByToken(IntPtr clazz, int token)
    {
        if (clazz == IntPtr.Zero)
            return NativeStructUtils.GetMethodInfoForMissingMethod(token.ToString());

        var iter = IntPtr.Zero;
        IntPtr method;
        while ((method = il2cpp_class_get_methods(clazz, ref iter)) != IntPtr.Zero)
            if (il2cpp_method_get_token(method) == token)
                return method;

        var className = il2cpp_class_get_name_(clazz);
        Logger.Instance.LogTrace("Unable to find method {ClassName}::{Token}", className, token);

        return NativeStructUtils.GetMethodInfoForMissingMethod(className + "::" + token);
    }

    public static IntPtr GetIl2CppMethod(IntPtr clazz, bool isGeneric, string methodName, string returnTypeName,
        params string[] argTypes)
    {
        if (clazz == IntPtr.Zero)
            return NativeStructUtils.GetMethodInfoForMissingMethod(methodName + "(" + string.Join(", ", argTypes) +
                                                                   ")");

        returnTypeName = Regex.Replace(returnTypeName, "\\`\\d+", "").Replace('/', '.').Replace('+', '.');
        for (var index = 0; index < argTypes.Length; index++)
        {
            var argType = argTypes[index];
            argTypes[index] = Regex.Replace(argType, "\\`\\d+", "").Replace('/', '.').Replace('+', '.');
        }

        var methodsSeen = 0;
        var lastMethod = IntPtr.Zero;
        var iter = IntPtr.Zero;
        IntPtr method;
        while ((method = il2cpp_class_get_methods(clazz, ref iter)) != IntPtr.Zero)
        {
            if (il2cpp_method_get_name_(method) != methodName)
                continue;

            if (il2cpp_method_get_param_count(method) != argTypes.Length)
                continue;

            if (il2cpp_method_is_generic(method) != isGeneric)
                continue;

            var returnType = il2cpp_method_get_return_type(method);
            var returnTypeNameActual = il2cpp_type_get_name_(returnType);
            if (returnTypeNameActual != returnTypeName)
                continue;

            methodsSeen++;
            lastMethod = method;

            var badType = false;
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = il2cpp_method_get_param(method, (uint)i);
                var typeName = il2cpp_type_get_name_(paramType);
                if (typeName != argTypes[i])
                {
                    badType = true;
                    break;
                }
            }

            if (badType) continue;

            return method;
        }

        var className = il2cpp_class_get_name_(clazz);

        if (methodsSeen == 1)
        {
            Logger.Instance.LogTrace(
                "Method {ClassName}::{MethodName} was stubbed with a random matching method of the same name", className, methodName);
            Logger.Instance.LogTrace(
                "Stubby return type/target: {LastMethod} / {ReturnTypeName}", il2cpp_type_get_name_(il2cpp_method_get_return_type(lastMethod)), returnTypeName);
            Logger.Instance.LogTrace("Stubby parameter types/targets follow:");
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = il2cpp_method_get_param(lastMethod, (uint)i);
                var typeName = il2cpp_type_get_name_(paramType);
                Logger.Instance.LogTrace("    {TypeName} / {ArgType}", typeName, argTypes[i]);
            }

            return lastMethod;
        }

        Logger.Instance.LogTrace("Unable to find method {ClassName}::{MethodName}; signature follows", className, methodName);
        Logger.Instance.LogTrace("    return {ReturnTypeName}", returnTypeName);
        foreach (var argType in argTypes)
            Logger.Instance.LogTrace("    {ArgType}", argType);
        Logger.Instance.LogTrace("Available methods of this name follow:");
        iter = IntPtr.Zero;
        while ((method = il2cpp_class_get_methods(clazz, ref iter)) != IntPtr.Zero)
        {
            if (il2cpp_method_get_name_(method) != methodName)
                continue;

            var nParams = il2cpp_method_get_param_count(method);
            Logger.Instance.LogTrace("Method starts");
            Logger.Instance.LogTrace(
                "     return {MethodTypeName}", il2cpp_type_get_name_(il2cpp_method_get_return_type(method)));
            for (var i = 0; i < nParams; i++)
            {
                var paramType = il2cpp_method_get_param(method, (uint)i);
                var typeName = il2cpp_type_get_name_(paramType);
                Logger.Instance.LogTrace("    {TypeName}", typeName);
            }

            return method;
        }

        return NativeStructUtils.GetMethodInfoForMissingMethod(className + "::" + methodName + "(" +
                                                               string.Join(", ", argTypes) + ")");
    }

    public static IntPtr GetIl2CppGenericInstanceMethod(IntPtr methodInfoPointer, IntPtr declaringTypeClassPointer, params IntPtr[] genericMethodArguments)
    {
        var types = new Il2CppSystem.Type[genericMethodArguments.Length];
        for (var i = 0; i < genericMethodArguments.Length; i++)
        {
            types[i] = Il2CppSystem.Type.internal_from_handle(il2cpp_class_get_type(genericMethodArguments[i]));
        }
        var methodInfo = new Il2CppSystem.Reflection.MethodInfo(il2cpp_method_get_object(methodInfoPointer, declaringTypeClassPointer));
        return il2cpp_method_get_from_reflection(Il2CppObjectBaseToPtrNotNull(methodInfo.MakeGenericMethod(types)));
    }

    public static IntPtr GetIl2CppGenericInstanceType(IntPtr typeClassPointer, params IntPtr[] genericTypeArguments)
    {
        var types = new Il2CppSystem.Type[genericTypeArguments.Length];
        for (var i = 0; i < genericTypeArguments.Length; i++)
        {
            types[i] = Il2CppSystem.Type.internal_from_handle(il2cpp_class_get_type(genericTypeArguments[i]));
        }
        return il2cpp_class_from_type(Il2CppSystem.Type.internal_from_handle(il2cpp_class_get_type(typeClassPointer)).MakeGenericType(types).TypeHandle.value);
    }

    public static ObjectPointer NewObjectPointer<T>()
    {
        return (ObjectPointer)il2cpp_object_new(Il2CppClassPointerStore<T>.NativeClassPtr);
    }

    public static string? Il2CppStringToManaged(IntPtr il2CppString)
    {
        if (il2CppString == IntPtr.Zero) return null;

        var length = il2cpp_string_length(il2CppString);
        var chars = il2cpp_string_chars(il2CppString);

        return new string(chars, 0, length);
    }

    public static IntPtr ManagedStringToIl2Cpp(string? str)
    {
        if (str == null) return IntPtr.Zero;

        fixed (char* chars = str)
        {
            return il2cpp_string_new_utf16(chars, str.Length);
        }
    }

    public static IntPtr Il2CppObjectBaseToPtr(Il2CppObjectBase obj)
    {
        return obj?.Pointer ?? IntPtr.Zero;
    }

    public static IntPtr Il2CppObjectBaseToPtrNotNull(Il2CppObjectBase obj)
    {
        return obj?.Pointer ?? throw new NullReferenceException();
    }

    public static IntPtr GetIl2CppNestedType(IntPtr enclosingType, string nestedTypeName)
    {
        if (enclosingType == IntPtr.Zero) return IntPtr.Zero;

        var iter = IntPtr.Zero;
        IntPtr nestedTypePtr;
        if (il2cpp_class_is_inflated(enclosingType))
        {
            Logger.Instance.LogTrace("Original class was inflated, falling back to reflection");

            return RuntimeReflectionHelper.GetNestedTypeViaReflection(enclosingType, nestedTypeName);
        }

        while ((nestedTypePtr = il2cpp_class_get_nested_types(enclosingType, ref iter)) != IntPtr.Zero)
            if (il2cpp_class_get_name_(nestedTypePtr) == nestedTypeName)
                return nestedTypePtr;

        Logger.Instance.LogError(
            "Nested type {NestedTypeName} on {EnclosingTypeName} not found!", nestedTypeName, il2cpp_class_get_name_(enclosingType));

        return IntPtr.Zero;
    }

    public static T ResolveICall<T>(string signature) where T : Delegate
    {
        var icallPtr = il2cpp_resolve_icall(signature);
        if (icallPtr == IntPtr.Zero)
        {
            Logger.Instance.LogTrace("ICall {Signature} not resolved", signature);
            return GenerateDelegateForMissingICall<T>(signature);
        }

        return GenerateDelegateForICall<T>(icallPtr);
    }

    private static T GenerateDelegateForMissingICall<T>(string signature) where T : Delegate
    {
        var invoke = typeof(T).GetMethod("Invoke")!;

        var trampoline = new DynamicMethod("(missing icall delegate) " + typeof(T).FullName,
            invoke.ReturnType, invoke.GetParameters().Select(it => it.ParameterType).ToArray(), typeof(IL2CPP), true);
        var bodyBuilder = trampoline.GetILGenerator();

        bodyBuilder.Emit(OpCodes.Ldstr, $"ICall with signature {signature} was not resolved");
        bodyBuilder.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor([typeof(string)])!);
        bodyBuilder.Emit(OpCodes.Throw);

        return (T)trampoline.CreateDelegate(typeof(T));
    }

    private static T GenerateDelegateForICall<T>(IntPtr icallPtr) where T : Delegate
    {
        var invoke = typeof(T).GetMethod("Invoke")!;

        var trampoline = new DynamicMethod("(icall delegate) " + typeof(T).FullName,
            invoke.ReturnType, invoke.GetParameters().Select(it => it.ParameterType).ToArray(), typeof(IL2CPP), true);
        var bodyBuilder = trampoline.GetILGenerator();

        bodyBuilder.Emit(OpCodes.Ldstr, "ICall delegate generation has not been implemented");
        bodyBuilder.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor([typeof(string)])!);
        bodyBuilder.Emit(OpCodes.Throw);

        return (T)trampoline.CreateDelegate(typeof(T));
    }

    public static T? PointerToValueGeneric<T>(IntPtr objectPointer, bool isFieldPointer, bool valueTypeWouldBeBoxed)
    {
        if (isFieldPointer)
        {
            if (il2cpp_class_is_valuetype(Il2CppClassPointerStore<T>.NativeClassPtr))
                objectPointer = il2cpp_value_box(Il2CppClassPointerStore<T>.NativeClassPtr, objectPointer);
            else
                objectPointer = *(IntPtr*)objectPointer;
        }

        if (!valueTypeWouldBeBoxed && il2cpp_class_is_valuetype(Il2CppClassPointerStore<T>.NativeClassPtr))
            objectPointer = il2cpp_value_box(Il2CppClassPointerStore<T>.NativeClassPtr, objectPointer);

        if (typeof(T) == typeof(string))
            return (T?)(object?)Il2CppStringToManaged(objectPointer);

        if (objectPointer == IntPtr.Zero)
            return default;

        if (typeof(T).IsValueType)
            return Il2CppObjectBase.UnboxUnsafe<T>(objectPointer);

        return Il2CppObjectPool.Get<T>(objectPointer);
    }

    // this is called if there's no actual il2cpp_gc_wbarrier_set_field()
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FieldWriteWbarrierStub(IntPtr obj, IntPtr targetAddress, IntPtr value)
    {
        // ignore obj
        *(IntPtr*)targetAddress = value;
    }

    // IL2CPP Functions
    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_init(IntPtr domain_name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_init_utf16(IntPtr domain_name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_shutdown();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_config_dir(IntPtr config_path);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_data_dir(IntPtr data_path);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_temp_dir(IntPtr temp_path);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_commandline_arguments(int argc, IntPtr argv, IntPtr basedir);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_commandline_arguments_utf16(int argc, IntPtr argv, IntPtr basedir);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_config_utf16(IntPtr executablePath);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_config(IntPtr executablePath);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_memory_callbacks(IntPtr callbacks);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_get_corlib();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_add_internal_call(IntPtr name, IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_resolve_icall([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_alloc(uint size);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_free(IntPtr ptr);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_array_class_get(IntPtr element_class, uint rank);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_array_length(IntPtr array);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_array_get_byte_length(IntPtr array);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_array_new(IntPtr elementTypeInfo, ulong length);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_array_new_specific(IntPtr arrayTypeInfo, ulong length);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_array_new_full(IntPtr array_class, ref ulong lengths, ref ulong lower_bounds);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_bounded_array_class_get(IntPtr element_class, uint rank,
        [MarshalAs(UnmanagedType.I1)] bool bounded);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_array_element_size(IntPtr array_class);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_assembly_get_image(IntPtr assembly);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_enum_basetype(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_generic(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_inflated(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_assignable_from(IntPtr klass, IntPtr oklass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_subclass_of(IntPtr klass, IntPtr klassc,
        [MarshalAs(UnmanagedType.I1)] bool check_interfaces);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_has_parent(IntPtr klass, IntPtr klassc);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_from_il2cpp_type(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_from_name(IntPtr image, [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_from_system_type(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_element_class(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_events(IntPtr klass, ref IntPtr iter);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_fields(IntPtr klass, ref IntPtr iter);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_nested_types(IntPtr klass, ref IntPtr iter);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_interfaces(IntPtr klass, ref IntPtr iter);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_properties(IntPtr klass, ref IntPtr iter);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_property_from_name(IntPtr klass, IntPtr name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_field_from_name(IntPtr klass,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_methods(IntPtr klass, ref IntPtr iter);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_method_from_name(IntPtr klass,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int argsCount);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_class_get_name(IntPtr klass);

    public static string? il2cpp_class_get_name_(IntPtr klass)
        => Marshal.PtrToStringUTF8(il2cpp_class_get_name(klass));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_class_get_namespace(IntPtr klass);

    public static string? il2cpp_class_get_namespace_(IntPtr klass)
        => Marshal.PtrToStringUTF8(il2cpp_class_get_namespace(klass));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_parent(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_declaring_type(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_class_instance_size(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_class_num_fields(IntPtr enumKlass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_valuetype(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_class_value_size(IntPtr klass, ref uint align);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_blittable(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_class_get_flags(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_abstract(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_interface(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_class_array_element_size(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_from_type(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_type(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_class_get_type_token(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_has_attribute(IntPtr klass, IntPtr attr_class);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_has_references(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_class_is_enum(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_class_get_image(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_class_get_assemblyname(IntPtr klass);

    public static string? il2cpp_class_get_assemblyname_(IntPtr klass)
        => Marshal.PtrToStringUTF8(il2cpp_class_get_assemblyname(klass));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_class_get_rank(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_class_get_bitmap_size(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_class_get_bitmap(IntPtr klass, ref uint bitmap);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_stats_dump_to_file(IntPtr path);

    //[DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    //public extern static ulong il2cpp_stats_get_value(IL2CPP_Stat stat);
    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_domain_get();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_domain_assembly_open(IntPtr domain, IntPtr name);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr* il2cpp_domain_get_assemblies(IntPtr domain, ref uint size);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr
        il2cpp_exception_from_name_msg(IntPtr image, IntPtr name_space, IntPtr name, IntPtr msg);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_get_exception_argument_null(IntPtr arg);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_format_exception(IntPtr ex, void* message, int message_size);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_format_stack_trace(IntPtr ex, void* output, int output_size);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_unhandled_exception(IntPtr ex);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_field_get_flags(IntPtr field);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_field_get_name(IntPtr field);

    public static string? il2cpp_field_get_name_(IntPtr field)
        => Marshal.PtrToStringUTF8(il2cpp_field_get_name(field));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_field_get_parent(IntPtr field);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_field_get_offset(IntPtr field);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_field_get_type(IntPtr field);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_field_get_value(IntPtr obj, IntPtr field, void* value);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_field_get_value_object(IntPtr field, IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_field_has_attribute(IntPtr field, IntPtr attr_class);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_field_set_value(IntPtr obj, IntPtr field, void* value);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_field_static_get_value(IntPtr field, void* value);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_field_static_set_value(IntPtr field, void* value);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_field_set_value_object(IntPtr instance, IntPtr field, IntPtr value);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_gc_collect(int maxGenerations);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_gc_collect_a_little();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_gc_disable();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_gc_enable();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_gc_is_disabled();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern long il2cpp_gc_get_used_size();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern long il2cpp_gc_get_heap_size();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_gc_wbarrier_set_field(IntPtr obj, IntPtr targetAddress, IntPtr gcObj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_gchandle_new(IntPtr obj, [MarshalAs(UnmanagedType.I1)] bool pinned);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_gchandle_new_weakref(IntPtr obj,
        [MarshalAs(UnmanagedType.I1)] bool track_resurrection);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_gchandle_get_target(nint gchandle);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_gchandle_free(nint gchandle);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_unity_liveness_calculation_begin(IntPtr filter, int max_object_count,
        IntPtr callback, IntPtr userdata, IntPtr onWorldStarted, IntPtr onWorldStopped);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_unity_liveness_calculation_end(IntPtr state);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_unity_liveness_calculation_from_root(IntPtr root, IntPtr state);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_unity_liveness_calculation_from_statics(IntPtr state);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_method_get_return_type(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_method_get_declaring_type(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_method_get_name(IntPtr method);

    public static string? il2cpp_method_get_name_(IntPtr method)
        => Marshal.PtrToStringUTF8(il2cpp_method_get_name(method));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr il2cpp_method_get_from_reflection(IntPtr method)
    {
        if (UnityVersionHandler.HasGetMethodFromReflection) return _il2cpp_method_get_from_reflection(method);
        Il2CppReflectionMethod* reflectionMethod = (Il2CppReflectionMethod*)method;
        return (IntPtr)reflectionMethod->method;
    }

    [DllImport("GameAssembly", EntryPoint = nameof(il2cpp_method_get_from_reflection), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr _il2cpp_method_get_from_reflection(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_method_get_object(IntPtr method, IntPtr refclass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_method_is_generic(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_method_is_inflated(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_method_is_instance(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_method_get_param_count(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_method_get_param(IntPtr method, uint index);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_method_get_class(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_method_has_attribute(IntPtr method, IntPtr attr_class);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_method_get_flags(IntPtr method, ref uint iflags);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_method_get_token(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_method_get_param_name(IntPtr method, uint index);

    public static string? il2cpp_method_get_param_name_(IntPtr method, uint index)
        => Marshal.PtrToStringUTF8(il2cpp_method_get_param_name(method, index));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_profiler_install(IntPtr prof, IntPtr shutdown_callback);

    // [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    // public extern static void il2cpp_profiler_set_events(IL2CPP_ProfileFlags events);
    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_profiler_install_enter_leave(IntPtr enter, IntPtr fleave);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_profiler_install_allocation(IntPtr callback);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_profiler_install_gc(IntPtr callback, IntPtr heap_resize_callback);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_profiler_install_fileio(IntPtr callback);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_profiler_install_thread(IntPtr start, IntPtr end);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_property_get_flags(IntPtr prop);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_property_get_get_method(IntPtr prop);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_property_get_set_method(IntPtr prop);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_property_get_name(IntPtr prop);

    public static string? il2cpp_property_get_name_(IntPtr prop)
        => Marshal.PtrToStringUTF8(il2cpp_property_get_name(prop));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_property_get_parent(IntPtr prop);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_object_get_class(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_object_get_size(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_object_get_virtual_method(IntPtr obj, IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_object_new(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_object_unbox(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_value_box(IntPtr klass, IntPtr data);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_monitor_enter(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_monitor_try_enter(IntPtr obj, uint timeout);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_monitor_exit(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_monitor_pulse(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_monitor_pulse_all(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_monitor_wait(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_monitor_try_wait(IntPtr obj, uint timeout);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_runtime_invoke(IntPtr method, IntPtr obj, void** param, ref IntPtr exc);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    // param can be of Il2CppObject*
    public static extern IntPtr il2cpp_runtime_invoke_convert_args(IntPtr method, IntPtr obj, void** param,
        int paramCount, ref IntPtr exc);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_runtime_class_init(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_runtime_object_init(IntPtr obj);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_runtime_object_init_exception(IntPtr obj, ref IntPtr exc);

    // [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    // public extern static void il2cpp_runtime_unhandled_exception_policy_set(IL2CPP_RuntimeUnhandledExceptionPolicy value);
    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_string_length(IntPtr str);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern char* il2cpp_string_chars(IntPtr str);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_string_new(string str);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_string_new_len(string str, uint length);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_string_new_utf16(char* text, int len);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_string_new_wrapper(string str);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_string_intern(string str);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_string_is_interned(string str);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_thread_current();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_thread_attach(IntPtr domain);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_thread_detach(IntPtr thread);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void** il2cpp_thread_get_all_attached_threads(ref uint size);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_is_vm_thread(IntPtr thread);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_current_thread_walk_frame_stack(IntPtr func, IntPtr user_data);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_thread_walk_frame_stack(IntPtr thread, IntPtr func, IntPtr user_data);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_current_thread_get_top_frame(IntPtr frame);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_thread_get_top_frame(IntPtr thread, IntPtr frame);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_current_thread_get_frame_at(int offset, IntPtr frame);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_thread_get_frame_at(IntPtr thread, int offset, IntPtr frame);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_current_thread_get_stack_depth();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_thread_get_stack_depth(IntPtr thread);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_type_get_object(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int il2cpp_type_get_type(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_type_get_class_or_element_class(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_type_get_name(IntPtr type);

    public static string? il2cpp_type_get_name_(IntPtr type)
        => Marshal.PtrToStringUTF8(il2cpp_type_get_name(type));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_type_is_byref(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_type_get_attrs(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_type_equals(IntPtr type, IntPtr otherType);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_type_get_assembly_qualified_name(IntPtr type);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_image_get_assembly(IntPtr image);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_image_get_name(IntPtr image);

    public static string? il2cpp_image_get_name_(IntPtr image)
        => Marshal.PtrToStringUTF8(il2cpp_image_get_name(image));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint il2cpp_image_get_filename(IntPtr image);

    public static string? il2cpp_image_get_filename_(IntPtr image)
        => Marshal.PtrToStringUTF8(il2cpp_image_get_filename(image));

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_image_get_entry_point(IntPtr image);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint il2cpp_image_get_class_count(IntPtr image);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_image_get_class(IntPtr image, uint index);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_capture_memory_snapshot();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_free_captured_memory_snapshot(IntPtr snapshot);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_set_find_plugin_callback(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_register_log_callback(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_debugger_set_agent_options(IntPtr options);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_is_debugger_attached();

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_unity_install_unitytls_interface(void* unitytlsInterfaceStruct);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_custom_attrs_from_class(IntPtr klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_custom_attrs_from_method(IntPtr method);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_custom_attrs_get_attr(IntPtr ainfo, IntPtr attr_klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool il2cpp_custom_attrs_has_attr(IntPtr ainfo, IntPtr attr_klass);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr il2cpp_custom_attrs_construct(IntPtr cinfo);

    [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void il2cpp_custom_attrs_free(IntPtr ainfo);
}
