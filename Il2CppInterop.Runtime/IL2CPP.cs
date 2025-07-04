using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

public static unsafe class IL2CPP
{
    private static readonly Dictionary<string, IntPtr> ourImagesMap = new();

    static IL2CPP()
    {
        LoadIl2CppAPIs();
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
            var name = Marshal.PtrToStringAnsi(il2cpp_image_get_name(image));
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
                "Field {FieldName} was not found on class {ClassName}", fieldName, Marshal.PtrToStringUTF8(il2cpp_class_get_name(clazz)));
        return field;
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

        var className = Marshal.PtrToStringAnsi(il2cpp_class_get_name(clazz));
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
            if (Marshal.PtrToStringAnsi(il2cpp_method_get_name(method)) != methodName)
                continue;

            if (il2cpp_method_get_param_count(method) != argTypes.Length)
                continue;

            if (il2cpp_method_is_generic(method) != isGeneric)
                continue;

            var returnType = il2cpp_method_get_return_type(method);
            var returnTypeNameActual = Marshal.PtrToStringAnsi(il2cpp_type_get_name(returnType));
            if (returnTypeNameActual != returnTypeName)
                continue;

            methodsSeen++;
            lastMethod = method;

            var badType = false;
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = il2cpp_method_get_param(method, (uint)i);
                var typeName = Marshal.PtrToStringAnsi(il2cpp_type_get_name(paramType));
                if (typeName != argTypes[i])
                {
                    badType = true;
                    break;
                }
            }

            if (badType) continue;

            return method;
        }

        var className = Marshal.PtrToStringAnsi(il2cpp_class_get_name(clazz));

        if (methodsSeen == 1)
        {
            Logger.Instance.LogTrace(
                "Method {ClassName}::{MethodName} was stubbed with a random matching method of the same name", className, methodName);
            Logger.Instance.LogTrace(
                "Stubby return type/target: {LastMethod} / {ReturnTypeName}", Marshal.PtrToStringUTF8(il2cpp_type_get_name(il2cpp_method_get_return_type(lastMethod))), returnTypeName);
            Logger.Instance.LogTrace("Stubby parameter types/targets follow:");
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = il2cpp_method_get_param(lastMethod, (uint)i);
                var typeName = Marshal.PtrToStringAnsi(il2cpp_type_get_name(paramType));
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
            if (Marshal.PtrToStringAnsi(il2cpp_method_get_name(method)) != methodName)
                continue;

            var nParams = il2cpp_method_get_param_count(method);
            Logger.Instance.LogTrace("Method starts");
            Logger.Instance.LogTrace(
                "     return {MethodTypeName}", Marshal.PtrToStringUTF8(il2cpp_type_get_name(il2cpp_method_get_return_type(method))));
            for (var i = 0; i < nParams; i++)
            {
                var paramType = il2cpp_method_get_param(method, (uint)i);
                var typeName = Marshal.PtrToStringAnsi(il2cpp_type_get_name(paramType));
                Logger.Instance.LogTrace("    {TypeName}", typeName);
            }

            return method;
        }

        return NativeStructUtils.GetMethodInfoForMissingMethod(className + "::" + methodName + "(" +
                                                               string.Join(", ", argTypes) + ")");
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
            if (Marshal.PtrToStringAnsi(il2cpp_class_get_name(nestedTypePtr)) == nestedTypeName)
                return nestedTypePtr;

        Logger.Instance.LogError(
            "Nested type {NestedTypeName} on {EnclosingTypeName} not found!", nestedTypeName, Marshal.PtrToStringUTF8(il2cpp_class_get_name(enclosingType)));

        return IntPtr.Zero;
    }

    public static void ThrowIfNull(object arg)
    {
        if (arg == null)
            throw new NullReferenceException();
    }

    public static T ResolveICall<T>(string signature) where T : Delegate
    {
        var icallPtr = il2cpp_resolve_icall(signature);
        if (icallPtr == IntPtr.Zero)
        {
            Logger.Instance.LogTrace("ICall {Signature} not resolved", signature);
            return GenerateDelegateForMissingICall<T>(signature);
        }

        return Marshal.GetDelegateForFunctionPointer<T>(icallPtr);
    }

    private static T GenerateDelegateForMissingICall<T>(string signature) where T : Delegate
    {
        var invoke = typeof(T).GetMethod("Invoke")!;

        var trampoline = new DynamicMethod("(missing icall delegate) " + typeof(T).FullName,
            invoke.ReturnType, invoke.GetParameters().Select(it => it.ParameterType).ToArray(), typeof(IL2CPP), true);
        var bodyBuilder = trampoline.GetILGenerator();

        bodyBuilder.Emit(OpCodes.Ldstr, $"ICall with signature {signature} was not resolved");
        bodyBuilder.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] { typeof(string) })!);
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
            return (T)(object)Il2CppStringToManaged(objectPointer);

        if (objectPointer == IntPtr.Zero)
            return default;

        if (typeof(T).IsValueType)
            return Il2CppObjectBase.UnboxUnsafe<T>(objectPointer);

        return Il2CppObjectPool.Get<T>(objectPointer);
    }

    public static string RenderTypeName<T>(bool addRefMarker = false)
    {
        return RenderTypeName(typeof(T), addRefMarker);
    }

    public static string RenderTypeName(Type t, bool addRefMarker = false)
    {
        if (addRefMarker) return RenderTypeName(t) + "&";
        if (t.IsArray) return RenderTypeName(t.GetElementType()) + "[]";
        if (t.IsByRef) return RenderTypeName(t.GetElementType()) + "&";
        if (t.IsPointer) return RenderTypeName(t.GetElementType()) + "*";
        if (t.IsGenericParameter) return t.Name;

        if (t.IsGenericType)
        {
            if (t.TypeHasIl2CppArrayBase())
                return RenderTypeName(t.GetGenericArguments()[0]) + "[]";

            var builder = new StringBuilder();
            builder.Append(t.GetGenericTypeDefinition().FullNameObfuscated().TrimIl2CppPrefix());
            builder.Append('<');
            var genericArguments = t.GetGenericArguments();
            for (var i = 0; i < genericArguments.Length; i++)
            {
                if (i != 0) builder.Append(',');
                builder.Append(RenderTypeName(genericArguments[i]));
            }

            builder.Append('>');
            return builder.ToString();
        }

        if (t == typeof(Il2CppStringArray))
            return "System.String[]";

        return t.FullNameObfuscated().TrimIl2CppPrefix();
    }

    private static string FullNameObfuscated(this Type t)
    {
        var obfuscatedNameAnnotations = t.GetCustomAttribute<ObfuscatedNameAttribute>();
        if (obfuscatedNameAnnotations == null) return t.FullName;
        return obfuscatedNameAnnotations.ObfuscatedName;
    }

    private static string TrimIl2CppPrefix(this string s)
    {
        return s.StartsWith("Il2Cpp") ? s.Substring("Il2Cpp".Length) : s;
    }

    private static bool TypeHasIl2CppArrayBase(this Type type)
    {
        if (type == null) return false;
        if (type.IsConstructedGenericType) type = type.GetGenericTypeDefinition();
        if (type == typeof(Il2CppArrayBase<>)) return true;
        return TypeHasIl2CppArrayBase(type.BaseType);
    }

    // this is called if there's no actual il2cpp_gc_wbarrier_set_field()
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FieldWriteWbarrierStub(IntPtr obj, IntPtr targetAddress, IntPtr value)
    {
        // ignore obj
        *(IntPtr*)targetAddress = value;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    private static IntPtr gameAssembly;
    private static void Load<T>(string name, out T field) where T : Delegate
    {
        IntPtr ptr = GetProcAddress(gameAssembly, name);
        if (ptr == IntPtr.Zero)
        {
            Logger.Instance.LogWarning($"SEVERE WARNING: Failed to find export: {name}, Program may crash abruptly!");
            field = null;
            return;
        }
        field = Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    // IL2CPP Functions
    // === Delegates ===
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_init_delegate(IntPtr domain_name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_init_utf16_delegate(IntPtr domain_name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_shutdown_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_config_dir_delegate(IntPtr config_path);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_data_dir_delegate(IntPtr data_path);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_temp_dir_delegate(IntPtr temp_path);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_commandline_arguments_delegate(int argc, IntPtr argv, IntPtr basedir);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_commandline_arguments_utf16_delegate(int argc, IntPtr argv, IntPtr basedir);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_config_utf16_delegate(IntPtr executablePath);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_config_delegate(IntPtr executablePath);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_memory_callbacks_delegate(IntPtr callbacks);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_get_corlib_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_add_internal_call_delegate(IntPtr name, IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr il2cpp_resolve_icall_delegate(
        [MarshalAs(UnmanagedType.LPStr)] string name
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_alloc_delegate(uint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_free_delegate(IntPtr ptr);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_array_class_get_delegate(IntPtr element_class, uint rank);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_array_length_delegate(IntPtr array);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_array_get_byte_length_delegate(IntPtr array);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_array_new_delegate(IntPtr elementTypeInfo, ulong length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_array_new_specific_delegate(IntPtr arrayTypeInfo, ulong length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_array_new_full_delegate(IntPtr array_class, ref ulong lengths, ref ulong lower_bounds);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_array_element_size_delegate(IntPtr array_class);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_assembly_get_image_delegate(IntPtr assembly);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_enum_basetype_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_generic_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_inflated_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_assignable_from_delegate(IntPtr klass, IntPtr oklass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_has_parent_delegate(IntPtr klass, IntPtr klassc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_from_il2cpp_type_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr il2cpp_class_from_name_delegate(
        IntPtr image,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_from_system_type_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_element_class_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_events_delegate(IntPtr klass, ref IntPtr iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_fields_delegate(IntPtr klass, ref IntPtr iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr il2cpp_class_get_field_from_name_delegate(
        IntPtr klass,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_nested_types_delegate(IntPtr klass, ref IntPtr iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_interfaces_delegate(IntPtr klass, ref IntPtr iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_properties_delegate(IntPtr klass, ref IntPtr iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_property_from_name_delegate(IntPtr klass, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_methods_delegate(IntPtr klass, ref IntPtr iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_name_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_namespace_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_parent_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_declaring_type_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_class_instance_size_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_class_num_fields_delegate(IntPtr enumKlass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_valuetype_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_class_value_size_delegate(IntPtr klass, ref uint align);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_blittable_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_class_get_flags_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_abstract_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_interface_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_class_array_element_size_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_from_type_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_type_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_class_get_type_token_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_has_attribute_delegate(IntPtr klass, IntPtr attr_class);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_has_references_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_enum_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_image_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_class_get_assemblyname_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_class_get_rank_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_class_get_bitmap_size_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_class_get_bitmap_delegate(IntPtr klass, ref uint bitmap);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_stats_dump_to_file_delegate(IntPtr path);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_domain_get_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_domain_assembly_open_delegate(IntPtr domain, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr* il2cpp_domain_get_assemblies_delegate(IntPtr domain, ref uint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_exception_from_name_msg_delegate(IntPtr image, IntPtr name_space, IntPtr name, IntPtr msg);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_get_exception_argument_null_delegate(IntPtr arg);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_format_exception_delegate(IntPtr ex, void* message, int message_size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_format_stack_trace_delegate(IntPtr ex, void* output, int output_size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_unhandled_exception_delegate(IntPtr ex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_field_get_flags_delegate(IntPtr field);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_field_get_name_delegate(IntPtr field);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_field_get_parent_delegate(IntPtr field);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_field_get_offset_delegate(IntPtr field);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_field_get_type_delegate(IntPtr field);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_field_get_value_delegate(IntPtr obj, IntPtr field, void* value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_field_get_value_object_delegate(IntPtr field, IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_field_has_attribute_delegate(IntPtr field, IntPtr attr_class);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_field_set_value_delegate(IntPtr obj, IntPtr field, void* value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_field_static_get_value_delegate(IntPtr field, void* value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_field_static_set_value_delegate(IntPtr field, void* value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_field_set_value_object_delegate(IntPtr instance, IntPtr field, IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_gc_collect_delegate(int maxGenerations);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_gc_collect_a_little_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_gc_disable_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_gc_enable_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_gc_is_disabled_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long il2cpp_gc_get_used_size_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long il2cpp_gc_get_heap_size_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_gc_wbarrier_set_field_delegate(IntPtr obj, IntPtr targetAddress, IntPtr gcObj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_gchandle_get_target_delegate(nint gchandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_gchandle_free_delegate(nint gchandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_unity_liveness_calculation_begin_delegate(IntPtr filter, int max_object_count,
        IntPtr callback, IntPtr userdata, IntPtr onWorldStarted, IntPtr onWorldStopped);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_unity_liveness_calculation_end_delegate(IntPtr state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_unity_liveness_calculation_from_root_delegate(IntPtr root, IntPtr state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_unity_liveness_calculation_from_statics_delegate(IntPtr state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_return_type_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_declaring_type_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_name_delegate(IntPtr method);


    private delegate IntPtr _il2cpp_method_get_from_reflection_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_object_delegate(IntPtr method, IntPtr refclass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_method_is_generic_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_method_is_inflated_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_method_is_instance_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_method_get_param_count_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_param_delegate(IntPtr method, uint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_class_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_method_has_attribute_delegate(IntPtr method, IntPtr attr_class);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_method_get_flags_delegate(IntPtr method, ref uint iflags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_method_get_token_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_method_get_param_name_delegate(IntPtr method, uint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_profiler_install_delegate(IntPtr prof, IntPtr shutdown_callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_profiler_install_enter_leave_delegate(IntPtr enter, IntPtr fleave);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_profiler_install_allocation_delegate(IntPtr callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_profiler_install_gc_delegate(IntPtr callback, IntPtr heap_resize_callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_profiler_install_fileio_delegate(IntPtr callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_profiler_install_thread_delegate(IntPtr start, IntPtr end);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_property_get_flags_delegate(IntPtr prop);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_property_get_get_method_delegate(IntPtr prop);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_property_get_set_method_delegate(IntPtr prop);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_property_get_name_delegate(IntPtr prop);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_property_get_parent_delegate(IntPtr prop);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_object_get_class_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_object_get_size_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_object_get_virtual_method_delegate(IntPtr obj, IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_object_new_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_object_unbox_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_value_box_delegate(IntPtr klass, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_monitor_enter_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_monitor_try_enter_delegate(IntPtr obj, uint timeout);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_monitor_exit_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_monitor_pulse_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_monitor_pulse_all_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_monitor_wait_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_monitor_try_wait_delegate(IntPtr obj, uint timeout);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_runtime_invoke_delegate(IntPtr method, IntPtr obj, void** param, ref IntPtr exc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_runtime_class_init_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_runtime_object_init_delegate(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_runtime_object_init_exception_delegate(IntPtr obj, ref IntPtr exc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_string_length_delegate(IntPtr str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate char* il2cpp_string_chars_delegate(IntPtr str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_string_new_delegate(string str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_string_new_len_delegate(string str, uint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_string_new_utf16_delegate(char* text, int len);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_string_new_wrapper_delegate(string str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_string_intern_delegate(string str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_string_is_interned_delegate(string str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_thread_current_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_thread_attach_delegate(IntPtr domain);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_thread_detach_delegate(IntPtr thread);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void** il2cpp_thread_get_all_attached_threads_delegate(ref uint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_is_vm_thread_delegate(IntPtr thread);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_current_thread_walk_frame_stack_delegate(IntPtr func, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_thread_walk_frame_stack_delegate(IntPtr thread, IntPtr func, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_current_thread_get_top_frame_delegate(IntPtr frame);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_thread_get_top_frame_delegate(IntPtr thread, IntPtr frame);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_current_thread_get_frame_at_delegate(int offset, IntPtr frame);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_thread_get_frame_at_delegate(IntPtr thread, int offset, IntPtr frame);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_current_thread_get_stack_depth_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_thread_get_stack_depth_delegate(IntPtr thread);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_type_get_object_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int il2cpp_type_get_type_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_type_get_class_or_element_class_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_type_get_name_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_type_is_byref_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_type_get_attrs_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_type_equals_delegate(IntPtr type, IntPtr otherType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_type_get_assembly_qualified_name_delegate(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_image_get_assembly_delegate(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_image_get_name_delegate(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_image_get_filename_delegate(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_image_get_entry_point_delegate(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint il2cpp_image_get_class_count_delegate(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_image_get_class_delegate(IntPtr image, uint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_capture_memory_snapshot_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_free_captured_memory_snapshot_delegate(IntPtr snapshot);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_set_find_plugin_callback_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_register_log_callback_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_debugger_set_agent_options_delegate(IntPtr options);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_is_debugger_attached_delegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_unity_install_unitytls_interface_delegate(void* unitytlsInterfaceStruct);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_custom_attrs_from_class_delegate(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_custom_attrs_from_method_delegate(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_custom_attrs_get_attr_delegate(IntPtr ainfo, IntPtr attr_klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_custom_attrs_has_attr_delegate(IntPtr ainfo, IntPtr attr_klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr il2cpp_custom_attrs_construct_delegate(IntPtr cinfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void il2cpp_custom_attrs_free_delegate(IntPtr ainfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr il2cpp_bounded_array_class_get_delegate(IntPtr element_class, uint rank, [MarshalAs(UnmanagedType.I1)] bool bounded);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr il2cpp_class_get_method_from_name_delegate(IntPtr klass, [MarshalAs(UnmanagedType.LPStr)] string name, int argsCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool il2cpp_class_is_subclass_of_delegate(IntPtr klass, IntPtr klassc, [MarshalAs(UnmanagedType.I1)] bool check_interfaces);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate nint il2cpp_gchandle_new_delegate(IntPtr obj, [MarshalAs(UnmanagedType.I1)] bool pinned);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate nint il2cpp_gchandle_new_weakref_delegate(IntPtr obj, [MarshalAs(UnmanagedType.I1)] bool track_resurrection);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr il2cpp_runtime_invoke_convert_args_delegate(IntPtr method, IntPtr obj, void** param, int paramCount, ref IntPtr exc);

    // === Fields ===
    private static il2cpp_init_delegate _il2cpp_init;
    private static il2cpp_init_utf16_delegate _il2cpp_init_utf16;
    private static il2cpp_shutdown_delegate _il2cpp_shutdown;
    private static il2cpp_set_config_dir_delegate _il2cpp_set_config_dir;
    private static il2cpp_set_data_dir_delegate _il2cpp_set_data_dir;
    private static il2cpp_set_temp_dir_delegate _il2cpp_set_temp_dir;
    private static il2cpp_set_commandline_arguments_delegate _il2cpp_set_commandline_arguments;
    private static il2cpp_set_commandline_arguments_utf16_delegate _il2cpp_set_commandline_arguments_utf16;
    private static il2cpp_set_config_utf16_delegate _il2cpp_set_config_utf16;
    private static il2cpp_set_config_delegate _il2cpp_set_config;
    private static il2cpp_set_memory_callbacks_delegate _il2cpp_set_memory_callbacks;
    private static il2cpp_get_corlib_delegate _il2cpp_get_corlib;
    private static il2cpp_add_internal_call_delegate _il2cpp_add_internal_call;
    private static il2cpp_resolve_icall_delegate _il2cpp_resolve_icall;
    private static il2cpp_alloc_delegate _il2cpp_alloc;
    private static il2cpp_free_delegate _il2cpp_free;
    private static il2cpp_array_class_get_delegate _il2cpp_array_class_get;
    private static il2cpp_array_length_delegate _il2cpp_array_length;
    private static il2cpp_array_get_byte_length_delegate _il2cpp_array_get_byte_length;
    private static il2cpp_array_new_delegate _il2cpp_array_new;
    private static il2cpp_array_new_specific_delegate _il2cpp_array_new_specific;
    private static il2cpp_array_new_full_delegate _il2cpp_array_new_full;
    private static il2cpp_array_element_size_delegate _il2cpp_array_element_size;
    private static il2cpp_assembly_get_image_delegate _il2cpp_assembly_get_image;
    private static il2cpp_class_enum_basetype_delegate _il2cpp_class_enum_basetype;
    private static il2cpp_class_is_generic_delegate _il2cpp_class_is_generic;
    private static il2cpp_class_is_inflated_delegate _il2cpp_class_is_inflated;
    private static il2cpp_class_is_assignable_from_delegate _il2cpp_class_is_assignable_from;
    private static il2cpp_class_has_parent_delegate _il2cpp_class_has_parent;
    private static il2cpp_class_from_il2cpp_type_delegate _il2cpp_class_from_il2cpp_type;
    private static il2cpp_class_from_name_delegate _il2cpp_class_from_name;
    private static il2cpp_class_from_system_type_delegate _il2cpp_class_from_system_type;
    private static il2cpp_class_get_element_class_delegate _il2cpp_class_get_element_class;
    private static il2cpp_class_get_events_delegate _il2cpp_class_get_events;
    private static il2cpp_class_get_fields_delegate _il2cpp_class_get_fields;
    private static il2cpp_class_get_field_from_name_delegate _il2cpp_class_get_field_from_name;
    private static il2cpp_class_get_nested_types_delegate _il2cpp_class_get_nested_types;
    private static il2cpp_class_get_interfaces_delegate _il2cpp_class_get_interfaces;
    private static il2cpp_class_get_properties_delegate _il2cpp_class_get_properties;
    private static il2cpp_class_get_property_from_name_delegate _il2cpp_class_get_property_from_name;
    private static il2cpp_class_get_methods_delegate _il2cpp_class_get_methods;
    private static il2cpp_class_get_name_delegate _il2cpp_class_get_name;
    private static il2cpp_class_get_namespace_delegate _il2cpp_class_get_namespace;
    private static il2cpp_class_get_parent_delegate _il2cpp_class_get_parent;
    private static il2cpp_class_get_declaring_type_delegate _il2cpp_class_get_declaring_type;
    private static il2cpp_class_instance_size_delegate _il2cpp_class_instance_size;
    private static il2cpp_class_num_fields_delegate _il2cpp_class_num_fields;
    private static il2cpp_class_is_valuetype_delegate _il2cpp_class_is_valuetype;
    private static il2cpp_class_value_size_delegate _il2cpp_class_value_size;
    private static il2cpp_class_is_blittable_delegate _il2cpp_class_is_blittable;
    private static il2cpp_class_get_flags_delegate _il2cpp_class_get_flags;
    private static il2cpp_class_is_abstract_delegate _il2cpp_class_is_abstract;
    private static il2cpp_class_is_interface_delegate _il2cpp_class_is_interface;
    private static il2cpp_class_array_element_size_delegate _il2cpp_class_array_element_size;
    private static il2cpp_class_from_type_delegate _il2cpp_class_from_type;
    private static il2cpp_class_get_type_delegate _il2cpp_class_get_type;
    private static il2cpp_class_get_type_token_delegate _il2cpp_class_get_type_token;
    private static il2cpp_class_has_attribute_delegate _il2cpp_class_has_attribute;
    private static il2cpp_class_has_references_delegate _il2cpp_class_has_references;
    private static il2cpp_class_is_enum_delegate _il2cpp_class_is_enum;
    private static il2cpp_class_get_image_delegate _il2cpp_class_get_image;
    private static il2cpp_class_get_assemblyname_delegate _il2cpp_class_get_assemblyname;
    private static il2cpp_class_get_rank_delegate _il2cpp_class_get_rank;
    private static il2cpp_class_get_bitmap_size_delegate _il2cpp_class_get_bitmap_size;
    private static il2cpp_class_get_bitmap_delegate _il2cpp_class_get_bitmap;
    private static il2cpp_stats_dump_to_file_delegate _il2cpp_stats_dump_to_file;
    private static il2cpp_domain_get_delegate _il2cpp_domain_get;
    private static il2cpp_domain_assembly_open_delegate _il2cpp_domain_assembly_open;
    private static il2cpp_domain_get_assemblies_delegate _il2cpp_domain_get_assemblies;
    private static il2cpp_exception_from_name_msg_delegate _il2cpp_exception_from_name_msg;
    private static il2cpp_get_exception_argument_null_delegate _il2cpp_get_exception_argument_null;
    private static il2cpp_format_exception_delegate _il2cpp_format_exception;
    private static il2cpp_format_stack_trace_delegate _il2cpp_format_stack_trace;
    private static il2cpp_unhandled_exception_delegate _il2cpp_unhandled_exception;
    private static il2cpp_field_get_flags_delegate _il2cpp_field_get_flags;
    private static il2cpp_field_get_name_delegate _il2cpp_field_get_name;
    private static il2cpp_field_get_parent_delegate _il2cpp_field_get_parent;
    private static il2cpp_field_get_offset_delegate _il2cpp_field_get_offset;
    private static il2cpp_field_get_type_delegate _il2cpp_field_get_type;
    private static il2cpp_field_get_value_delegate _il2cpp_field_get_value;
    private static il2cpp_field_get_value_object_delegate _il2cpp_field_get_value_object;
    private static il2cpp_field_has_attribute_delegate _il2cpp_field_has_attribute;
    private static il2cpp_field_set_value_delegate _il2cpp_field_set_value;
    private static il2cpp_field_static_get_value_delegate _il2cpp_field_static_get_value;
    private static il2cpp_field_static_set_value_delegate _il2cpp_field_static_set_value;
    private static il2cpp_field_set_value_object_delegate _il2cpp_field_set_value_object;
    private static il2cpp_gc_collect_delegate _il2cpp_gc_collect;
    private static il2cpp_gc_collect_a_little_delegate _il2cpp_gc_collect_a_little;
    private static il2cpp_gc_disable_delegate _il2cpp_gc_disable;
    private static il2cpp_gc_enable_delegate _il2cpp_gc_enable;
    private static il2cpp_gc_is_disabled_delegate _il2cpp_gc_is_disabled;
    private static il2cpp_gc_get_used_size_delegate _il2cpp_gc_get_used_size;
    private static il2cpp_gc_get_heap_size_delegate _il2cpp_gc_get_heap_size;
    private static il2cpp_gc_wbarrier_set_field_delegate _il2cpp_gc_wbarrier_set_field;
    private static il2cpp_gchandle_get_target_delegate _il2cpp_gchandle_get_target;
    private static il2cpp_gchandle_free_delegate _il2cpp_gchandle_free;
    private static il2cpp_unity_liveness_calculation_begin_delegate _il2cpp_unity_liveness_calculation_begin;
    private static il2cpp_unity_liveness_calculation_end_delegate _il2cpp_unity_liveness_calculation_end;
    private static il2cpp_unity_liveness_calculation_from_root_delegate _il2cpp_unity_liveness_calculation_from_root;
    private static il2cpp_unity_liveness_calculation_from_statics_delegate _il2cpp_unity_liveness_calculation_from_statics;
    private static il2cpp_method_get_return_type_delegate _il2cpp_method_get_return_type;
    private static il2cpp_method_get_declaring_type_delegate _il2cpp_method_get_declaring_type;
    private static il2cpp_method_get_name_delegate _il2cpp_method_get_name;
    private static _il2cpp_method_get_from_reflection_delegate __il2cpp_method_get_from_reflection;
    private static il2cpp_method_get_object_delegate _il2cpp_method_get_object;
    private static il2cpp_method_is_generic_delegate _il2cpp_method_is_generic;
    private static il2cpp_method_is_inflated_delegate _il2cpp_method_is_inflated;
    private static il2cpp_method_is_instance_delegate _il2cpp_method_is_instance;
    private static il2cpp_method_get_param_count_delegate _il2cpp_method_get_param_count;
    private static il2cpp_method_get_param_delegate _il2cpp_method_get_param;
    private static il2cpp_method_get_class_delegate _il2cpp_method_get_class;
    private static il2cpp_method_has_attribute_delegate _il2cpp_method_has_attribute;
    private static il2cpp_method_get_flags_delegate _il2cpp_method_get_flags;
    private static il2cpp_method_get_token_delegate _il2cpp_method_get_token;
    private static il2cpp_method_get_param_name_delegate _il2cpp_method_get_param_name;
    private static il2cpp_profiler_install_delegate _il2cpp_profiler_install;
    private static il2cpp_profiler_install_enter_leave_delegate _il2cpp_profiler_install_enter_leave;
    private static il2cpp_profiler_install_allocation_delegate _il2cpp_profiler_install_allocation;
    private static il2cpp_profiler_install_gc_delegate _il2cpp_profiler_install_gc;
    private static il2cpp_profiler_install_fileio_delegate _il2cpp_profiler_install_fileio;
    private static il2cpp_profiler_install_thread_delegate _il2cpp_profiler_install_thread;
    private static il2cpp_property_get_flags_delegate _il2cpp_property_get_flags;
    private static il2cpp_property_get_get_method_delegate _il2cpp_property_get_get_method;
    private static il2cpp_property_get_set_method_delegate _il2cpp_property_get_set_method;
    private static il2cpp_property_get_name_delegate _il2cpp_property_get_name;
    private static il2cpp_property_get_parent_delegate _il2cpp_property_get_parent;
    private static il2cpp_object_get_class_delegate _il2cpp_object_get_class;
    private static il2cpp_object_get_size_delegate _il2cpp_object_get_size;
    private static il2cpp_object_get_virtual_method_delegate _il2cpp_object_get_virtual_method;
    private static il2cpp_object_new_delegate _il2cpp_object_new;
    private static il2cpp_object_unbox_delegate _il2cpp_object_unbox;
    private static il2cpp_value_box_delegate _il2cpp_value_box;
    private static il2cpp_monitor_enter_delegate _il2cpp_monitor_enter;
    private static il2cpp_monitor_try_enter_delegate _il2cpp_monitor_try_enter;
    private static il2cpp_monitor_exit_delegate _il2cpp_monitor_exit;
    private static il2cpp_monitor_pulse_delegate _il2cpp_monitor_pulse;
    private static il2cpp_monitor_pulse_all_delegate _il2cpp_monitor_pulse_all;
    private static il2cpp_monitor_wait_delegate _il2cpp_monitor_wait;
    private static il2cpp_monitor_try_wait_delegate _il2cpp_monitor_try_wait;
    private static il2cpp_runtime_invoke_delegate _il2cpp_runtime_invoke;
    private static il2cpp_runtime_class_init_delegate _il2cpp_runtime_class_init;
    private static il2cpp_runtime_object_init_delegate _il2cpp_runtime_object_init;
    private static il2cpp_runtime_object_init_exception_delegate _il2cpp_runtime_object_init_exception;
    private static il2cpp_string_length_delegate _il2cpp_string_length;
    private static il2cpp_string_chars_delegate _il2cpp_string_chars;
    private static il2cpp_string_new_delegate _il2cpp_string_new;
    private static il2cpp_string_new_len_delegate _il2cpp_string_new_len;
    private static il2cpp_string_new_utf16_delegate _il2cpp_string_new_utf16;
    private static il2cpp_string_new_wrapper_delegate _il2cpp_string_new_wrapper;
    private static il2cpp_string_intern_delegate _il2cpp_string_intern;
    private static il2cpp_string_is_interned_delegate _il2cpp_string_is_interned;
    private static il2cpp_thread_current_delegate _il2cpp_thread_current;
    private static il2cpp_thread_attach_delegate _il2cpp_thread_attach;
    private static il2cpp_thread_detach_delegate _il2cpp_thread_detach;
    private static il2cpp_thread_get_all_attached_threads_delegate _il2cpp_thread_get_all_attached_threads;
    private static il2cpp_is_vm_thread_delegate _il2cpp_is_vm_thread;
    private static il2cpp_current_thread_walk_frame_stack_delegate _il2cpp_current_thread_walk_frame_stack;
    private static il2cpp_thread_walk_frame_stack_delegate _il2cpp_thread_walk_frame_stack;
    private static il2cpp_current_thread_get_top_frame_delegate _il2cpp_current_thread_get_top_frame;
    private static il2cpp_thread_get_top_frame_delegate _il2cpp_thread_get_top_frame;
    private static il2cpp_current_thread_get_frame_at_delegate _il2cpp_current_thread_get_frame_at;
    private static il2cpp_thread_get_frame_at_delegate _il2cpp_thread_get_frame_at;
    private static il2cpp_current_thread_get_stack_depth_delegate _il2cpp_current_thread_get_stack_depth;
    private static il2cpp_thread_get_stack_depth_delegate _il2cpp_thread_get_stack_depth;
    private static il2cpp_type_get_object_delegate _il2cpp_type_get_object;
    private static il2cpp_type_get_type_delegate _il2cpp_type_get_type;
    private static il2cpp_type_get_class_or_element_class_delegate _il2cpp_type_get_class_or_element_class;
    private static il2cpp_type_get_name_delegate _il2cpp_type_get_name;
    private static il2cpp_type_is_byref_delegate _il2cpp_type_is_byref;
    private static il2cpp_type_get_attrs_delegate _il2cpp_type_get_attrs;
    private static il2cpp_type_equals_delegate _il2cpp_type_equals;
    private static il2cpp_type_get_assembly_qualified_name_delegate _il2cpp_type_get_assembly_qualified_name;
    private static il2cpp_image_get_assembly_delegate _il2cpp_image_get_assembly;
    private static il2cpp_image_get_name_delegate _il2cpp_image_get_name;
    private static il2cpp_image_get_filename_delegate _il2cpp_image_get_filename;
    private static il2cpp_image_get_entry_point_delegate _il2cpp_image_get_entry_point;
    private static il2cpp_image_get_class_count_delegate _il2cpp_image_get_class_count;
    private static il2cpp_image_get_class_delegate _il2cpp_image_get_class;
    private static il2cpp_capture_memory_snapshot_delegate _il2cpp_capture_memory_snapshot;
    private static il2cpp_free_captured_memory_snapshot_delegate _il2cpp_free_captured_memory_snapshot;
    private static il2cpp_set_find_plugin_callback_delegate _il2cpp_set_find_plugin_callback;
    private static il2cpp_register_log_callback_delegate _il2cpp_register_log_callback;
    private static il2cpp_debugger_set_agent_options_delegate _il2cpp_debugger_set_agent_options;
    private static il2cpp_is_debugger_attached_delegate _il2cpp_is_debugger_attached;
    private static il2cpp_unity_install_unitytls_interface_delegate _il2cpp_unity_install_unitytls_interface;
    private static il2cpp_custom_attrs_from_class_delegate _il2cpp_custom_attrs_from_class;
    private static il2cpp_custom_attrs_from_method_delegate _il2cpp_custom_attrs_from_method;
    private static il2cpp_custom_attrs_get_attr_delegate _il2cpp_custom_attrs_get_attr;
    private static il2cpp_custom_attrs_has_attr_delegate _il2cpp_custom_attrs_has_attr;
    private static il2cpp_custom_attrs_construct_delegate _il2cpp_custom_attrs_construct;
    private static il2cpp_custom_attrs_free_delegate _il2cpp_custom_attrs_free;
    private static il2cpp_bounded_array_class_get_delegate _il2cpp_bounded_array_class_get;
    private static il2cpp_class_get_method_from_name_delegate _il2cpp_class_get_method_from_name;
    private static il2cpp_class_is_subclass_of_delegate _il2cpp_class_is_subclass_of;
    private static il2cpp_gchandle_new_delegate _il2cpp_gchandle_new;
    private static il2cpp_gchandle_new_weakref_delegate _il2cpp_gchandle_new_weakref;
    private static il2cpp_runtime_invoke_convert_args_delegate _il2cpp_runtime_invoke_convert_args;

    // === Load Calls ===
    public static void LoadIl2CppAPIs()
    {
        gameAssembly = GetModuleHandleW("GameAssembly.dll");
        if (gameAssembly == IntPtr.Zero)
            throw new Exception("GameAssembly.dll not loaded.");

        Load("il2cpp_init", out _il2cpp_init);
        Load("il2cpp_init_utf16", out _il2cpp_init_utf16);
        Load("il2cpp_shutdown", out _il2cpp_shutdown);
        Load("il2cpp_set_config_dir", out _il2cpp_set_config_dir);
        Load("il2cpp_set_data_dir", out _il2cpp_set_data_dir);
        Load("il2cpp_set_temp_dir", out _il2cpp_set_temp_dir);
        Load("il2cpp_set_commandline_arguments", out _il2cpp_set_commandline_arguments);
        Load("il2cpp_set_commandline_arguments_utf16", out _il2cpp_set_commandline_arguments_utf16);
        Load("il2cpp_set_config_utf16", out _il2cpp_set_config_utf16);
        Load("il2cpp_set_config", out _il2cpp_set_config);
        Load("il2cpp_set_memory_callbacks", out _il2cpp_set_memory_callbacks);
        Load("il2cpp_get_corlib", out _il2cpp_get_corlib);
        Load("il2cpp_add_internal_call", out _il2cpp_add_internal_call);
        Load("il2cpp_resolve_icall", out _il2cpp_resolve_icall);
        Load("il2cpp_alloc", out _il2cpp_alloc);
        Load("il2cpp_free", out _il2cpp_free);
        Load("il2cpp_array_class_get", out _il2cpp_array_class_get);
        Load("il2cpp_array_length", out _il2cpp_array_length);
        Load("il2cpp_array_get_byte_length", out _il2cpp_array_get_byte_length);
        Load("il2cpp_array_new", out _il2cpp_array_new);
        Load("il2cpp_array_new_specific", out _il2cpp_array_new_specific);
        Load("il2cpp_array_new_full", out _il2cpp_array_new_full);
        Load("il2cpp_array_element_size", out _il2cpp_array_element_size);
        Load("il2cpp_assembly_get_image", out _il2cpp_assembly_get_image);
        Load("il2cpp_class_enum_basetype", out _il2cpp_class_enum_basetype);
        Load("il2cpp_class_is_generic", out _il2cpp_class_is_generic);
        Load("il2cpp_class_is_inflated", out _il2cpp_class_is_inflated);
        Load("il2cpp_class_is_assignable_from", out _il2cpp_class_is_assignable_from);
        Load("il2cpp_class_has_parent", out _il2cpp_class_has_parent);
        Load("il2cpp_class_from_il2cpp_type", out _il2cpp_class_from_il2cpp_type);
        Load("il2cpp_class_from_name", out _il2cpp_class_from_name);
        Load("il2cpp_class_from_system_type", out _il2cpp_class_from_system_type);
        Load("il2cpp_class_get_element_class", out _il2cpp_class_get_element_class);
        Load("il2cpp_class_get_events", out _il2cpp_class_get_events);
        Load("il2cpp_class_get_fields", out _il2cpp_class_get_fields);
        Load("il2cpp_class_get_field_from_name", out _il2cpp_class_get_field_from_name);
        Load("il2cpp_class_get_nested_types", out _il2cpp_class_get_nested_types);
        Load("il2cpp_class_get_interfaces", out _il2cpp_class_get_interfaces);
        Load("il2cpp_class_get_properties", out _il2cpp_class_get_properties);
        Load("il2cpp_class_get_property_from_name", out _il2cpp_class_get_property_from_name);
        Load("il2cpp_class_get_methods", out _il2cpp_class_get_methods);
        Load("il2cpp_class_get_name", out _il2cpp_class_get_name);
        Load("il2cpp_class_get_namespace", out _il2cpp_class_get_namespace);
        Load("il2cpp_class_get_parent", out _il2cpp_class_get_parent);
        Load("il2cpp_class_get_declaring_type", out _il2cpp_class_get_declaring_type);
        Load("il2cpp_class_instance_size", out _il2cpp_class_instance_size);
        Load("il2cpp_class_num_fields", out _il2cpp_class_num_fields);
        Load("il2cpp_class_is_valuetype", out _il2cpp_class_is_valuetype);
        Load("il2cpp_class_value_size", out _il2cpp_class_value_size);
        Load("il2cpp_class_is_blittable", out _il2cpp_class_is_blittable);
        Load("il2cpp_class_get_flags", out _il2cpp_class_get_flags);
        Load("il2cpp_class_is_abstract", out _il2cpp_class_is_abstract);
        Load("il2cpp_class_is_interface", out _il2cpp_class_is_interface);
        Load("il2cpp_class_array_element_size", out _il2cpp_class_array_element_size);
        Load("il2cpp_class_from_type", out _il2cpp_class_from_type);
        Load("il2cpp_class_get_type", out _il2cpp_class_get_type);
        Load("il2cpp_class_get_type_token", out _il2cpp_class_get_type_token);
        Load("il2cpp_class_has_attribute", out _il2cpp_class_has_attribute);
        Load("il2cpp_class_has_references", out _il2cpp_class_has_references);
        Load("il2cpp_class_is_enum", out _il2cpp_class_is_enum);
        Load("il2cpp_class_get_image", out _il2cpp_class_get_image);
        Load("il2cpp_class_get_assemblyname", out _il2cpp_class_get_assemblyname);
        Load("il2cpp_class_get_rank", out _il2cpp_class_get_rank);
        Load("il2cpp_class_get_bitmap_size", out _il2cpp_class_get_bitmap_size);
        Load("il2cpp_class_get_bitmap", out _il2cpp_class_get_bitmap);
        Load("il2cpp_stats_dump_to_file", out _il2cpp_stats_dump_to_file);
        Load("il2cpp_domain_get", out _il2cpp_domain_get);
        Load("il2cpp_domain_assembly_open", out _il2cpp_domain_assembly_open);
        Load("il2cpp_domain_get_assemblies", out _il2cpp_domain_get_assemblies);
        Load("il2cpp_exception_from_name_msg", out _il2cpp_exception_from_name_msg);
        Load("il2cpp_get_exception_argument_null", out _il2cpp_get_exception_argument_null);
        Load("il2cpp_format_exception", out _il2cpp_format_exception);
        Load("il2cpp_format_stack_trace", out _il2cpp_format_stack_trace);
        Load("il2cpp_unhandled_exception", out _il2cpp_unhandled_exception);
        Load("il2cpp_field_get_flags", out _il2cpp_field_get_flags);
        Load("il2cpp_field_get_name", out _il2cpp_field_get_name);
        Load("il2cpp_field_get_parent", out _il2cpp_field_get_parent);
        Load("il2cpp_field_get_offset", out _il2cpp_field_get_offset);
        Load("il2cpp_field_get_type", out _il2cpp_field_get_type);
        Load("il2cpp_field_get_value", out _il2cpp_field_get_value);
        Load("il2cpp_field_get_value_object", out _il2cpp_field_get_value_object);
        Load("il2cpp_field_has_attribute", out _il2cpp_field_has_attribute);
        Load("il2cpp_field_set_value", out _il2cpp_field_set_value);
        Load("il2cpp_field_static_get_value", out _il2cpp_field_static_get_value);
        Load("il2cpp_field_static_set_value", out _il2cpp_field_static_set_value);
        Load("il2cpp_field_set_value_object", out _il2cpp_field_set_value_object);
        Load("il2cpp_gc_collect", out _il2cpp_gc_collect);
        Load("il2cpp_gc_collect_a_little", out _il2cpp_gc_collect_a_little);
        Load("il2cpp_gc_disable", out _il2cpp_gc_disable);
        Load("il2cpp_gc_enable", out _il2cpp_gc_enable);
        Load("il2cpp_gc_is_disabled", out _il2cpp_gc_is_disabled);
        Load("il2cpp_gc_get_used_size", out _il2cpp_gc_get_used_size);
        Load("il2cpp_gc_get_heap_size", out _il2cpp_gc_get_heap_size);
        Load("il2cpp_gc_wbarrier_set_field", out _il2cpp_gc_wbarrier_set_field);
        Load("il2cpp_gchandle_get_target", out _il2cpp_gchandle_get_target);
        Load("il2cpp_gchandle_free", out _il2cpp_gchandle_free);
        Load("il2cpp_unity_liveness_calculation_begin", out _il2cpp_unity_liveness_calculation_begin);
        Load("il2cpp_unity_liveness_calculation_end", out _il2cpp_unity_liveness_calculation_end);
        Load("il2cpp_unity_liveness_calculation_from_root", out _il2cpp_unity_liveness_calculation_from_root);
        Load("il2cpp_unity_liveness_calculation_from_statics", out _il2cpp_unity_liveness_calculation_from_statics);
        Load("il2cpp_method_get_return_type", out _il2cpp_method_get_return_type);
        Load("il2cpp_method_get_declaring_type", out _il2cpp_method_get_declaring_type);
        Load("il2cpp_method_get_name", out _il2cpp_method_get_name);
        Load("il2cpp_method_get_from_reflection", out __il2cpp_method_get_from_reflection);
        Load("il2cpp_method_get_object", out _il2cpp_method_get_object);
        Load("il2cpp_method_is_generic", out _il2cpp_method_is_generic);
        Load("il2cpp_method_is_inflated", out _il2cpp_method_is_inflated);
        Load("il2cpp_method_is_instance", out _il2cpp_method_is_instance);
        Load("il2cpp_method_get_param_count", out _il2cpp_method_get_param_count);
        Load("il2cpp_method_get_param", out _il2cpp_method_get_param);
        Load("il2cpp_method_get_class", out _il2cpp_method_get_class);
        Load("il2cpp_method_has_attribute", out _il2cpp_method_has_attribute);
        Load("il2cpp_method_get_flags", out _il2cpp_method_get_flags);
        Load("il2cpp_method_get_token", out _il2cpp_method_get_token);
        Load("il2cpp_method_get_param_name", out _il2cpp_method_get_param_name);
        Load("il2cpp_profiler_install", out _il2cpp_profiler_install);
        Load("il2cpp_profiler_install_enter_leave", out _il2cpp_profiler_install_enter_leave);
        Load("il2cpp_profiler_install_allocation", out _il2cpp_profiler_install_allocation);
        Load("il2cpp_profiler_install_gc", out _il2cpp_profiler_install_gc);
        Load("il2cpp_profiler_install_fileio", out _il2cpp_profiler_install_fileio);
        Load("il2cpp_profiler_install_thread", out _il2cpp_profiler_install_thread);
        Load("il2cpp_property_get_flags", out _il2cpp_property_get_flags);
        Load("il2cpp_property_get_get_method", out _il2cpp_property_get_get_method);
        Load("il2cpp_property_get_set_method", out _il2cpp_property_get_set_method);
        Load("il2cpp_property_get_name", out _il2cpp_property_get_name);
        Load("il2cpp_property_get_parent", out _il2cpp_property_get_parent);
        Load("il2cpp_object_get_class", out _il2cpp_object_get_class);
        Load("il2cpp_object_get_size", out _il2cpp_object_get_size);
        Load("il2cpp_object_get_virtual_method", out _il2cpp_object_get_virtual_method);
        Load("il2cpp_object_new", out _il2cpp_object_new);
        Load("il2cpp_object_unbox", out _il2cpp_object_unbox);
        Load("il2cpp_value_box", out _il2cpp_value_box);
        Load("il2cpp_monitor_enter", out _il2cpp_monitor_enter);
        Load("il2cpp_monitor_try_enter", out _il2cpp_monitor_try_enter);
        Load("il2cpp_monitor_exit", out _il2cpp_monitor_exit);
        Load("il2cpp_monitor_pulse", out _il2cpp_monitor_pulse);
        Load("il2cpp_monitor_pulse_all", out _il2cpp_monitor_pulse_all);
        Load("il2cpp_monitor_wait", out _il2cpp_monitor_wait);
        Load("il2cpp_monitor_try_wait", out _il2cpp_monitor_try_wait);
        Load("il2cpp_runtime_invoke", out _il2cpp_runtime_invoke);
        Load("il2cpp_runtime_class_init", out _il2cpp_runtime_class_init);
        Load("il2cpp_runtime_object_init", out _il2cpp_runtime_object_init);
        Load("il2cpp_runtime_object_init_exception", out _il2cpp_runtime_object_init_exception);
        Load("il2cpp_string_length", out _il2cpp_string_length);
        Load("il2cpp_string_chars", out _il2cpp_string_chars);
        Load("il2cpp_string_new", out _il2cpp_string_new);
        Load("il2cpp_string_new_len", out _il2cpp_string_new_len);
        Load("il2cpp_string_new_utf16", out _il2cpp_string_new_utf16);
        Load("il2cpp_string_new_wrapper", out _il2cpp_string_new_wrapper);
        Load("il2cpp_string_intern", out _il2cpp_string_intern);
        Load("il2cpp_string_is_interned", out _il2cpp_string_is_interned);
        Load("il2cpp_thread_current", out _il2cpp_thread_current);
        Load("il2cpp_thread_attach", out _il2cpp_thread_attach);
        Load("il2cpp_thread_detach", out _il2cpp_thread_detach);
        Load("il2cpp_thread_get_all_attached_threads", out _il2cpp_thread_get_all_attached_threads);
        Load("il2cpp_is_vm_thread", out _il2cpp_is_vm_thread);
        Load("il2cpp_current_thread_walk_frame_stack", out _il2cpp_current_thread_walk_frame_stack);
        Load("il2cpp_thread_walk_frame_stack", out _il2cpp_thread_walk_frame_stack);
        Load("il2cpp_current_thread_get_top_frame", out _il2cpp_current_thread_get_top_frame);
        Load("il2cpp_thread_get_top_frame", out _il2cpp_thread_get_top_frame);
        Load("il2cpp_current_thread_get_frame_at", out _il2cpp_current_thread_get_frame_at);
        Load("il2cpp_thread_get_frame_at", out _il2cpp_thread_get_frame_at);
        Load("il2cpp_current_thread_get_stack_depth", out _il2cpp_current_thread_get_stack_depth);
        Load("il2cpp_thread_get_stack_depth", out _il2cpp_thread_get_stack_depth);
        Load("il2cpp_type_get_object", out _il2cpp_type_get_object);
        Load("il2cpp_type_get_type", out _il2cpp_type_get_type);
        Load("il2cpp_type_get_class_or_element_class", out _il2cpp_type_get_class_or_element_class);
        Load("il2cpp_type_get_name", out _il2cpp_type_get_name);
        Load("il2cpp_type_is_byref", out _il2cpp_type_is_byref);
        Load("il2cpp_type_get_attrs", out _il2cpp_type_get_attrs);
        Load("il2cpp_type_equals", out _il2cpp_type_equals);
        Load("il2cpp_type_get_assembly_qualified_name", out _il2cpp_type_get_assembly_qualified_name);
        Load("il2cpp_image_get_assembly", out _il2cpp_image_get_assembly);
        Load("il2cpp_image_get_name", out _il2cpp_image_get_name);
        Load("il2cpp_image_get_filename", out _il2cpp_image_get_filename);
        Load("il2cpp_image_get_entry_point", out _il2cpp_image_get_entry_point);
        Load("il2cpp_image_get_class_count", out _il2cpp_image_get_class_count);
        Load("il2cpp_image_get_class", out _il2cpp_image_get_class);
        Load("il2cpp_capture_memory_snapshot", out _il2cpp_capture_memory_snapshot);
        Load("il2cpp_free_captured_memory_snapshot", out _il2cpp_free_captured_memory_snapshot);
        Load("il2cpp_set_find_plugin_callback", out _il2cpp_set_find_plugin_callback);
        Load("il2cpp_register_log_callback", out _il2cpp_register_log_callback);
        Load("il2cpp_debugger_set_agent_options", out _il2cpp_debugger_set_agent_options);
        Load("il2cpp_is_debugger_attached", out _il2cpp_is_debugger_attached);
        Load("il2cpp_unity_install_unitytls_interface", out _il2cpp_unity_install_unitytls_interface);
        Load("il2cpp_custom_attrs_from_class", out _il2cpp_custom_attrs_from_class);
        Load("il2cpp_custom_attrs_from_method", out _il2cpp_custom_attrs_from_method);
        Load("il2cpp_custom_attrs_get_attr", out _il2cpp_custom_attrs_get_attr);
        Load("il2cpp_custom_attrs_has_attr", out _il2cpp_custom_attrs_has_attr);
        Load("il2cpp_custom_attrs_construct", out _il2cpp_custom_attrs_construct);
        Load("il2cpp_custom_attrs_free", out _il2cpp_custom_attrs_free);
        Load("il2cpp_bounded_array_class_get", out _il2cpp_bounded_array_class_get);
        Load("il2cpp_class_get_method_from_name", out _il2cpp_class_get_method_from_name);
        Load("il2cpp_class_is_subclass_of", out _il2cpp_class_is_subclass_of);
        Load("il2cpp_gchandle_new", out _il2cpp_gchandle_new);
        Load("il2cpp_gchandle_new_weakref", out _il2cpp_gchandle_new_weakref);
        Load("il2cpp_runtime_invoke_convert_args", out _il2cpp_runtime_invoke_convert_args);
    }

    // === Wrappers ===
    public static void il2cpp_init(IntPtr domain_name) => _il2cpp_init(domain_name);
    public static void il2cpp_init_utf16(IntPtr domain_name) => _il2cpp_init_utf16(domain_name);
    public static void il2cpp_shutdown() => _il2cpp_shutdown();
    public static void il2cpp_set_config_dir(IntPtr config_path) => _il2cpp_set_config_dir(config_path);
    public static void il2cpp_set_data_dir(IntPtr data_path) => _il2cpp_set_data_dir(data_path);
    public static void il2cpp_set_temp_dir(IntPtr temp_path) => _il2cpp_set_temp_dir(temp_path);
    public static void il2cpp_set_commandline_arguments(int argc, IntPtr argv, IntPtr basedir) => _il2cpp_set_commandline_arguments(argc, argv, basedir);
    public static void il2cpp_set_commandline_arguments_utf16(int argc, IntPtr argv, IntPtr basedir) => _il2cpp_set_commandline_arguments_utf16(argc, argv, basedir);
    public static void il2cpp_set_config_utf16(IntPtr executablePath) => _il2cpp_set_config_utf16(executablePath);
    public static void il2cpp_set_config(IntPtr executablePath) => _il2cpp_set_config(executablePath);
    public static void il2cpp_set_memory_callbacks(IntPtr callbacks) => _il2cpp_set_memory_callbacks(callbacks);
    public static IntPtr il2cpp_get_corlib() => _il2cpp_get_corlib();
    public static void il2cpp_add_internal_call(IntPtr name, IntPtr method) => _il2cpp_add_internal_call(name, method);
    public static IntPtr il2cpp_resolve_icall(string name) => _il2cpp_resolve_icall(name);
    public static IntPtr il2cpp_alloc(uint size) => _il2cpp_alloc(size);
    public static void il2cpp_free(IntPtr ptr) => _il2cpp_free(ptr);
    public static IntPtr il2cpp_array_class_get(IntPtr element_class, uint rank) => _il2cpp_array_class_get(element_class, rank);
    public static uint il2cpp_array_length(IntPtr array) => _il2cpp_array_length(array);
    public static uint il2cpp_array_get_byte_length(IntPtr array) => _il2cpp_array_get_byte_length(array);
    public static IntPtr il2cpp_array_new(IntPtr elementTypeInfo, ulong length) => _il2cpp_array_new(elementTypeInfo, length);
    public static IntPtr il2cpp_array_new_specific(IntPtr arrayTypeInfo, ulong length) => _il2cpp_array_new_specific(arrayTypeInfo, length);
    public static IntPtr il2cpp_array_new_full(IntPtr array_class, ref ulong lengths, ref ulong lower_bounds) => _il2cpp_array_new_full(array_class, ref lengths, ref lower_bounds);
    public static int il2cpp_array_element_size(IntPtr array_class) => _il2cpp_array_element_size(array_class);
    public static IntPtr il2cpp_assembly_get_image(IntPtr assembly) => _il2cpp_assembly_get_image(assembly);
    public static IntPtr il2cpp_class_enum_basetype(IntPtr klass) => _il2cpp_class_enum_basetype(klass);
    public static bool il2cpp_class_is_generic(IntPtr klass) => _il2cpp_class_is_generic(klass);
    public static bool il2cpp_class_is_inflated(IntPtr klass) => _il2cpp_class_is_inflated(klass);
    public static bool il2cpp_class_is_assignable_from(IntPtr klass, IntPtr oklass) => _il2cpp_class_is_assignable_from(klass, oklass);
    public static bool il2cpp_class_has_parent(IntPtr klass, IntPtr klassc) => _il2cpp_class_has_parent(klass, klassc);
    public static IntPtr il2cpp_class_from_il2cpp_type(IntPtr type) => _il2cpp_class_from_il2cpp_type(type);
    public static IntPtr il2cpp_class_from_name(IntPtr image, string namespaze, string name) => _il2cpp_class_from_name(image, namespaze, name);
    public static IntPtr il2cpp_class_from_system_type(IntPtr type) => _il2cpp_class_from_system_type(type);
    public static IntPtr il2cpp_class_get_element_class(IntPtr klass) => _il2cpp_class_get_element_class(klass);
    public static IntPtr il2cpp_class_get_events(IntPtr klass, ref IntPtr iter) => _il2cpp_class_get_events(klass, ref iter);
    public static IntPtr il2cpp_class_get_fields(IntPtr klass, ref IntPtr iter) => _il2cpp_class_get_fields(klass, ref iter);
    public static IntPtr il2cpp_class_get_field_from_name(IntPtr klass, string name) => _il2cpp_class_get_field_from_name(klass, name);
    public static IntPtr il2cpp_class_get_nested_types(IntPtr klass, ref IntPtr iter) => _il2cpp_class_get_nested_types(klass, ref iter);
    public static IntPtr il2cpp_class_get_interfaces(IntPtr klass, ref IntPtr iter) => _il2cpp_class_get_interfaces(klass, ref iter);
    public static IntPtr il2cpp_class_get_properties(IntPtr klass, ref IntPtr iter) => _il2cpp_class_get_properties(klass, ref iter);
    public static IntPtr il2cpp_class_get_property_from_name(IntPtr klass, IntPtr name) => _il2cpp_class_get_property_from_name(klass, name);
    public static IntPtr il2cpp_class_get_methods(IntPtr klass, ref IntPtr iter) => _il2cpp_class_get_methods(klass, ref iter);
    public static IntPtr il2cpp_class_get_name(IntPtr klass) => _il2cpp_class_get_name(klass);
    public static IntPtr il2cpp_class_get_namespace(IntPtr klass) => _il2cpp_class_get_namespace(klass);
    public static IntPtr il2cpp_class_get_parent(IntPtr klass) => _il2cpp_class_get_parent(klass);
    public static IntPtr il2cpp_class_get_declaring_type(IntPtr klass) => _il2cpp_class_get_declaring_type(klass);
    public static int il2cpp_class_instance_size(IntPtr klass) => _il2cpp_class_instance_size(klass);
    public static uint il2cpp_class_num_fields(IntPtr enumKlass) => _il2cpp_class_num_fields(enumKlass);
    public static bool il2cpp_class_is_valuetype(IntPtr klass) => _il2cpp_class_is_valuetype(klass);
    public static int il2cpp_class_value_size(IntPtr klass, ref uint align) => _il2cpp_class_value_size(klass, ref align);
    public static bool il2cpp_class_is_blittable(IntPtr klass) => _il2cpp_class_is_blittable(klass);
    public static int il2cpp_class_get_flags(IntPtr klass) => _il2cpp_class_get_flags(klass);
    public static bool il2cpp_class_is_abstract(IntPtr klass) => _il2cpp_class_is_abstract(klass);
    public static bool il2cpp_class_is_interface(IntPtr klass) => _il2cpp_class_is_interface(klass);
    public static int il2cpp_class_array_element_size(IntPtr klass) => _il2cpp_class_array_element_size(klass);
    public static IntPtr il2cpp_class_from_type(IntPtr type) => _il2cpp_class_from_type(type);
    public static IntPtr il2cpp_class_get_type(IntPtr klass) => _il2cpp_class_get_type(klass);
    public static uint il2cpp_class_get_type_token(IntPtr klass) => _il2cpp_class_get_type_token(klass);
    public static bool il2cpp_class_has_attribute(IntPtr klass, IntPtr attr_class) => _il2cpp_class_has_attribute(klass, attr_class);
    public static bool il2cpp_class_has_references(IntPtr klass) => _il2cpp_class_has_references(klass);
    public static bool il2cpp_class_is_enum(IntPtr klass) => _il2cpp_class_is_enum(klass);
    public static IntPtr il2cpp_class_get_image(IntPtr klass) => _il2cpp_class_get_image(klass);
    public static IntPtr il2cpp_class_get_assemblyname(IntPtr klass) => _il2cpp_class_get_assemblyname(klass);
    public static int il2cpp_class_get_rank(IntPtr klass) => _il2cpp_class_get_rank(klass);
    public static uint il2cpp_class_get_bitmap_size(IntPtr klass) => _il2cpp_class_get_bitmap_size(klass);
    public static void il2cpp_class_get_bitmap(IntPtr klass, ref uint bitmap) => _il2cpp_class_get_bitmap(klass, ref bitmap);
    public static bool il2cpp_stats_dump_to_file(IntPtr path) => _il2cpp_stats_dump_to_file(path);
    public static IntPtr il2cpp_domain_get() => _il2cpp_domain_get();
    public static IntPtr il2cpp_domain_assembly_open(IntPtr domain, IntPtr name) => _il2cpp_domain_assembly_open(domain, name);
    public static IntPtr* il2cpp_domain_get_assemblies(IntPtr domain, ref uint size) => _il2cpp_domain_get_assemblies(domain, ref size);
    public static IntPtr il2cpp_exception_from_name_msg(IntPtr image, IntPtr name_space, IntPtr name, IntPtr msg) => _il2cpp_exception_from_name_msg(image, name_space, name, msg);
    public static IntPtr il2cpp_get_exception_argument_null(IntPtr arg) => _il2cpp_get_exception_argument_null(arg);
    public static void il2cpp_format_exception(IntPtr ex, void* message, int message_size) => _il2cpp_format_exception(ex, message, message_size);
    public static void il2cpp_format_stack_trace(IntPtr ex, void* output, int output_size) => _il2cpp_format_stack_trace(ex, output, output_size);
    public static void il2cpp_unhandled_exception(IntPtr ex) => _il2cpp_unhandled_exception(ex);
    public static int il2cpp_field_get_flags(IntPtr field) => _il2cpp_field_get_flags(field);
    public static IntPtr il2cpp_field_get_name(IntPtr field) => _il2cpp_field_get_name(field);
    public static IntPtr il2cpp_field_get_parent(IntPtr field) => _il2cpp_field_get_parent(field);
    public static uint il2cpp_field_get_offset(IntPtr field) => _il2cpp_field_get_offset(field);
    public static IntPtr il2cpp_field_get_type(IntPtr field) => _il2cpp_field_get_type(field);
    public static void il2cpp_field_get_value(IntPtr obj, IntPtr field, void* value) => _il2cpp_field_get_value(obj, field, value);
    public static IntPtr il2cpp_field_get_value_object(IntPtr field, IntPtr obj) => _il2cpp_field_get_value_object(field, obj);
    public static bool il2cpp_field_has_attribute(IntPtr field, IntPtr attr_class) => _il2cpp_field_has_attribute(field, attr_class);
    public static void il2cpp_field_set_value(IntPtr obj, IntPtr field, void* value) => _il2cpp_field_set_value(obj, field, value);
    public static void il2cpp_field_static_get_value(IntPtr field, void* value) => _il2cpp_field_static_get_value(field, value);
    public static void il2cpp_field_static_set_value(IntPtr field, void* value) => _il2cpp_field_static_set_value(field, value);
    public static void il2cpp_field_set_value_object(IntPtr instance, IntPtr field, IntPtr value) => _il2cpp_field_set_value_object(instance, field, value);
    public static void il2cpp_gc_collect(int maxGenerations) => _il2cpp_gc_collect(maxGenerations);
    public static int il2cpp_gc_collect_a_little() => _il2cpp_gc_collect_a_little();
    public static void il2cpp_gc_disable() => _il2cpp_gc_disable();
    public static void il2cpp_gc_enable() => _il2cpp_gc_enable();
    public static bool il2cpp_gc_is_disabled() => _il2cpp_gc_is_disabled();
    public static long il2cpp_gc_get_used_size() => _il2cpp_gc_get_used_size();
    public static long il2cpp_gc_get_heap_size() => _il2cpp_gc_get_heap_size();
    public static void il2cpp_gc_wbarrier_set_field(IntPtr obj, IntPtr targetAddress, IntPtr gcObj) => _il2cpp_gc_wbarrier_set_field(obj, targetAddress, gcObj);
    public static IntPtr il2cpp_gchandle_get_target(nint gchandle) => _il2cpp_gchandle_get_target(gchandle);
    public static void il2cpp_gchandle_free(nint gchandle) => _il2cpp_gchandle_free(gchandle);
    public static IntPtr il2cpp_unity_liveness_calculation_begin(IntPtr filter, int max_object_count,
        IntPtr callback, IntPtr userdata, IntPtr onWorldStarted, IntPtr onWorldStopped) => _il2cpp_unity_liveness_calculation_begin(filter, max_object_count, callback, userdata, onWorldStarted, onWorldStopped);
    public static void il2cpp_unity_liveness_calculation_end(IntPtr state) => _il2cpp_unity_liveness_calculation_end(state);
    public static void il2cpp_unity_liveness_calculation_from_root(IntPtr root, IntPtr state) => _il2cpp_unity_liveness_calculation_from_root(root, state);
    public static void il2cpp_unity_liveness_calculation_from_statics(IntPtr state) => _il2cpp_unity_liveness_calculation_from_statics(state);
    public static IntPtr il2cpp_method_get_return_type(IntPtr method) => _il2cpp_method_get_return_type(method);
    public static IntPtr il2cpp_method_get_declaring_type(IntPtr method) => _il2cpp_method_get_declaring_type(method);
    public static IntPtr il2cpp_method_get_name(IntPtr method) => _il2cpp_method_get_name(method);
    public static IntPtr _il2cpp_method_get_from_reflection(IntPtr method) => __il2cpp_method_get_from_reflection(method);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr il2cpp_method_get_from_reflection(IntPtr method)
    {
        if (UnityVersionHandler.HasGetMethodFromReflection) return _il2cpp_method_get_from_reflection(method);
        Il2CppReflectionMethod* reflectionMethod = (Il2CppReflectionMethod*)method;
        return (IntPtr)reflectionMethod->method;
    }
    public static IntPtr il2cpp_method_get_object(IntPtr method, IntPtr refclass) => _il2cpp_method_get_object(method, refclass);
    public static bool il2cpp_method_is_generic(IntPtr method) => _il2cpp_method_is_generic(method);
    public static bool il2cpp_method_is_inflated(IntPtr method) => _il2cpp_method_is_inflated(method);
    public static bool il2cpp_method_is_instance(IntPtr method) => _il2cpp_method_is_instance(method);
    public static uint il2cpp_method_get_param_count(IntPtr method) => _il2cpp_method_get_param_count(method);
    public static IntPtr il2cpp_method_get_param(IntPtr method, uint index) => _il2cpp_method_get_param(method, index);
    public static IntPtr il2cpp_method_get_class(IntPtr method) => _il2cpp_method_get_class(method);
    public static bool il2cpp_method_has_attribute(IntPtr method, IntPtr attr_class) => _il2cpp_method_has_attribute(method, attr_class);
    public static uint il2cpp_method_get_flags(IntPtr method, ref uint iflags) => _il2cpp_method_get_flags(method, ref iflags);
    public static uint il2cpp_method_get_token(IntPtr method) => _il2cpp_method_get_token(method);
    public static IntPtr il2cpp_method_get_param_name(IntPtr method, uint index) => _il2cpp_method_get_param_name(method, index);
    public static void il2cpp_profiler_install(IntPtr prof, IntPtr shutdown_callback) => _il2cpp_profiler_install(prof, shutdown_callback);
    public static void il2cpp_profiler_install_enter_leave(IntPtr enter, IntPtr fleave) => _il2cpp_profiler_install_enter_leave(enter, fleave);
    public static void il2cpp_profiler_install_allocation(IntPtr callback) => _il2cpp_profiler_install_allocation(callback);
    public static void il2cpp_profiler_install_gc(IntPtr callback, IntPtr heap_resize_callback) => _il2cpp_profiler_install_gc(callback, heap_resize_callback);
    public static void il2cpp_profiler_install_fileio(IntPtr callback) => _il2cpp_profiler_install_fileio(callback);
    public static void il2cpp_profiler_install_thread(IntPtr start, IntPtr end) => _il2cpp_profiler_install_thread(start, end);
    public static uint il2cpp_property_get_flags(IntPtr prop) => _il2cpp_property_get_flags(prop);
    public static IntPtr il2cpp_property_get_get_method(IntPtr prop) => _il2cpp_property_get_get_method(prop);
    public static IntPtr il2cpp_property_get_set_method(IntPtr prop) => _il2cpp_property_get_set_method(prop);
    public static IntPtr il2cpp_property_get_name(IntPtr prop) => _il2cpp_property_get_name(prop);
    public static IntPtr il2cpp_property_get_parent(IntPtr prop) => _il2cpp_property_get_parent(prop);
    public static IntPtr il2cpp_object_get_class(IntPtr obj) => _il2cpp_object_get_class(obj);
    public static uint il2cpp_object_get_size(IntPtr obj) => _il2cpp_object_get_size(obj);
    public static IntPtr il2cpp_object_get_virtual_method(IntPtr obj, IntPtr method) => _il2cpp_object_get_virtual_method(obj, method);
    public static IntPtr il2cpp_object_new(IntPtr klass) => _il2cpp_object_new(klass);
    public static IntPtr il2cpp_object_unbox(IntPtr obj) => _il2cpp_object_unbox(obj);
    public static IntPtr il2cpp_value_box(IntPtr klass, IntPtr data) => _il2cpp_value_box(klass, data);
    public static void il2cpp_monitor_enter(IntPtr obj) => _il2cpp_monitor_enter(obj);
    public static bool il2cpp_monitor_try_enter(IntPtr obj, uint timeout) => _il2cpp_monitor_try_enter(obj, timeout);
    public static void il2cpp_monitor_exit(IntPtr obj) => _il2cpp_monitor_exit(obj);
    public static void il2cpp_monitor_pulse(IntPtr obj) => _il2cpp_monitor_pulse(obj);
    public static void il2cpp_monitor_pulse_all(IntPtr obj) => _il2cpp_monitor_pulse_all(obj);
    public static void il2cpp_monitor_wait(IntPtr obj) => _il2cpp_monitor_wait(obj);
    public static bool il2cpp_monitor_try_wait(IntPtr obj, uint timeout) => _il2cpp_monitor_try_wait(obj, timeout);
    public static IntPtr il2cpp_runtime_invoke(IntPtr method, IntPtr obj, void** param, ref IntPtr exc) => _il2cpp_runtime_invoke(method, obj, param, ref exc);
    public static void il2cpp_runtime_class_init(IntPtr klass) => _il2cpp_runtime_class_init(klass);
    public static void il2cpp_runtime_object_init(IntPtr obj) => _il2cpp_runtime_object_init(obj);
    public static void il2cpp_runtime_object_init_exception(IntPtr obj, ref IntPtr exc) => _il2cpp_runtime_object_init_exception(obj, ref exc);
    public static int il2cpp_string_length(IntPtr str) => _il2cpp_string_length(str);
    public static char* il2cpp_string_chars(IntPtr str) => _il2cpp_string_chars(str);
    public static IntPtr il2cpp_string_new(string str) => _il2cpp_string_new(str);
    public static IntPtr il2cpp_string_new_len(string str, uint length) => _il2cpp_string_new_len(str, length);
    public static IntPtr il2cpp_string_new_utf16(char* text, int len) => _il2cpp_string_new_utf16(text, len);
    public static IntPtr il2cpp_string_new_wrapper(string str) => _il2cpp_string_new_wrapper(str);
    public static IntPtr il2cpp_string_intern(string str) => _il2cpp_string_intern(str);
    public static IntPtr il2cpp_string_is_interned(string str) => _il2cpp_string_is_interned(str);
    public static IntPtr il2cpp_thread_current() => _il2cpp_thread_current();
    public static IntPtr il2cpp_thread_attach(IntPtr domain) => _il2cpp_thread_attach(domain);
    public static void il2cpp_thread_detach(IntPtr thread) => _il2cpp_thread_detach(thread);
    public static void** il2cpp_thread_get_all_attached_threads(ref uint size) => _il2cpp_thread_get_all_attached_threads(ref size);
    public static bool il2cpp_is_vm_thread(IntPtr thread) => _il2cpp_is_vm_thread(thread);
    public static void il2cpp_current_thread_walk_frame_stack(IntPtr func, IntPtr user_data) => _il2cpp_current_thread_walk_frame_stack(func, user_data);
    public static void il2cpp_thread_walk_frame_stack(IntPtr thread, IntPtr func, IntPtr user_data) => _il2cpp_thread_walk_frame_stack(thread, func, user_data);
    public static bool il2cpp_current_thread_get_top_frame(IntPtr frame) => _il2cpp_current_thread_get_top_frame(frame);
    public static bool il2cpp_thread_get_top_frame(IntPtr thread, IntPtr frame) => _il2cpp_thread_get_top_frame(thread, frame);
    public static bool il2cpp_current_thread_get_frame_at(int offset, IntPtr frame) => _il2cpp_current_thread_get_frame_at(offset, frame);
    public static bool il2cpp_thread_get_frame_at(IntPtr thread, int offset, IntPtr frame) => _il2cpp_thread_get_frame_at(thread, offset, frame);
    public static int il2cpp_current_thread_get_stack_depth() => _il2cpp_current_thread_get_stack_depth();
    public static int il2cpp_thread_get_stack_depth(IntPtr thread) => _il2cpp_thread_get_stack_depth(thread);
    public static IntPtr il2cpp_type_get_object(IntPtr type) => _il2cpp_type_get_object(type);
    public static int il2cpp_type_get_type(IntPtr type) => _il2cpp_type_get_type(type);
    public static IntPtr il2cpp_type_get_class_or_element_class(IntPtr type) => _il2cpp_type_get_class_or_element_class(type);
    public static IntPtr il2cpp_type_get_name(IntPtr type) => _il2cpp_type_get_name(type);
    public static bool il2cpp_type_is_byref(IntPtr type) => _il2cpp_type_is_byref(type);
    public static uint il2cpp_type_get_attrs(IntPtr type) => _il2cpp_type_get_attrs(type);
    public static bool il2cpp_type_equals(IntPtr type, IntPtr otherType) => _il2cpp_type_equals(type, otherType);
    public static IntPtr il2cpp_type_get_assembly_qualified_name(IntPtr type) => _il2cpp_type_get_assembly_qualified_name(type);
    public static IntPtr il2cpp_image_get_assembly(IntPtr image) => _il2cpp_image_get_assembly(image);
    public static IntPtr il2cpp_image_get_name(IntPtr image) => _il2cpp_image_get_name(image);
    public static IntPtr il2cpp_image_get_filename(IntPtr image) => _il2cpp_image_get_filename(image);
    public static IntPtr il2cpp_image_get_entry_point(IntPtr image) => _il2cpp_image_get_entry_point(image);
    public static uint il2cpp_image_get_class_count(IntPtr image) => _il2cpp_image_get_class_count(image);
    public static IntPtr il2cpp_image_get_class(IntPtr image, uint index) => _il2cpp_image_get_class(image, index);
    public static IntPtr il2cpp_capture_memory_snapshot() => _il2cpp_capture_memory_snapshot();
    public static void il2cpp_free_captured_memory_snapshot(IntPtr snapshot) => _il2cpp_free_captured_memory_snapshot(snapshot);
    public static void il2cpp_set_find_plugin_callback(IntPtr method) => _il2cpp_set_find_plugin_callback(method);
    public static void il2cpp_register_log_callback(IntPtr method) => _il2cpp_register_log_callback(method);
    public static void il2cpp_debugger_set_agent_options(IntPtr options) => _il2cpp_debugger_set_agent_options(options);
    public static bool il2cpp_is_debugger_attached() => _il2cpp_is_debugger_attached();
    public static void il2cpp_unity_install_unitytls_interface(void* unitytlsInterfaceStruct) => _il2cpp_unity_install_unitytls_interface(unitytlsInterfaceStruct);
    public static IntPtr il2cpp_custom_attrs_from_class(IntPtr klass) => _il2cpp_custom_attrs_from_class(klass);
    public static IntPtr il2cpp_custom_attrs_from_method(IntPtr method) => _il2cpp_custom_attrs_from_method(method);
    public static IntPtr il2cpp_custom_attrs_get_attr(IntPtr ainfo, IntPtr attr_klass) => _il2cpp_custom_attrs_get_attr(ainfo, attr_klass);
    public static bool il2cpp_custom_attrs_has_attr(IntPtr ainfo, IntPtr attr_klass) => _il2cpp_custom_attrs_has_attr(ainfo, attr_klass);
    public static IntPtr il2cpp_custom_attrs_construct(IntPtr cinfo) => _il2cpp_custom_attrs_construct(cinfo);
    public static void il2cpp_custom_attrs_free(IntPtr ainfo) => _il2cpp_custom_attrs_free(ainfo);
    public static IntPtr il2cpp_bounded_array_class_get(IntPtr element_class, uint rank, bool bounded) => _il2cpp_bounded_array_class_get(element_class, rank, bounded);
    public static IntPtr il2cpp_class_get_method_from_name(IntPtr klass, string name, int argsCount) => _il2cpp_class_get_method_from_name(klass, name, argsCount);
    public static bool il2cpp_class_is_subclass_of(IntPtr klass, IntPtr klassc, bool check_interfaces) => _il2cpp_class_is_subclass_of(klass, klassc, check_interfaces);
    public static nint il2cpp_gchandle_new(IntPtr obj, bool pinned) => _il2cpp_gchandle_new(obj, pinned);
    public static nint il2cpp_gchandle_new_weakref(IntPtr obj, bool track_resurrection) => _il2cpp_gchandle_new_weakref(obj, track_resurrection);
    public static IntPtr il2cpp_runtime_invoke_convert_args(IntPtr method, IntPtr obj, void** param, int paramCount, ref IntPtr exc) => _il2cpp_runtime_invoke_convert_args(method, obj, param, paramCount, ref exc);
}
