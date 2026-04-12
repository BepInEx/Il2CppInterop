using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

public static unsafe partial class IL2CPP
{
    private static readonly Dictionary<string, nint> ourImagesMap = new();

    static IL2CPP()
    {
        var domain = il2cpp_domain_get();
        if (domain == nint.Zero)
        {
            Logger.Instance.LogError("No il2cpp domain found; sad!");
            return;
        }

        uint assembliesCount = 0;
        var assemblies = il2cpp_domain_get_assemblies(domain, ref assembliesCount);
        for (var i = 0; i < assembliesCount; i++)
        {
            var image = il2cpp_assembly_get_image(assemblies[i]);
            var name = il2cpp_image_get_name(image)!;
            ourImagesMap[name] = image;
        }
    }

    internal static nint GetIl2CppImage(string name)
    {
        if (ourImagesMap.ContainsKey(name)) return ourImagesMap[name];
        return nint.Zero;
    }

    internal static nint[] GetIl2CppImages()
    {
        return ourImagesMap.Values.ToArray();
    }

    public static nint GetIl2CppClass(string assemblyName, string namespaze, string className)
    {
        if (!ourImagesMap.TryGetValue(assemblyName, out var image))
        {
            Logger.Instance.LogError("Assembly {AssemblyName} is not registered in il2cpp", assemblyName);
            return nint.Zero;
        }

        var clazz = il2cpp_class_from_name(image, namespaze, className);
        return clazz;
    }

    public static nint GetIl2CppField(nint clazz, string fieldName)
    {
        if (clazz == nint.Zero) return nint.Zero;

        var field = il2cpp_class_get_field_from_name(clazz, fieldName);
        if (field == nint.Zero)
            Logger.Instance.LogError(
                "Field {FieldName} was not found on class {ClassName}", fieldName, il2cpp_class_get_name(clazz));
        return field;
    }

    public static int GetIl2cppValueSize(nint klass)
    {
        uint align = 0;
        return il2cpp_class_value_size(klass, ref align);
    }

    public static nint GetIl2CppMethodByToken(nint clazz, int token)
    {
        if (clazz == nint.Zero)
            return NativeStructUtils.GetMethodInfoForMissingMethod(token.ToString());

        var iter = nint.Zero;
        nint method;
        while ((method = il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
            if (il2cpp_method_get_token(method) == token)
                return method;

        var className = il2cpp_class_get_name(clazz);
        Logger.Instance.LogTrace("Unable to find method {ClassName}::{Token}", className, token);

        return NativeStructUtils.GetMethodInfoForMissingMethod(className + "::" + token);
    }

    public static nint GetIl2CppMethod(nint clazz, bool isGeneric, string methodName, string returnTypeName,
        params string[] argTypes)
    {
        if (clazz == nint.Zero)
            return NativeStructUtils.GetMethodInfoForMissingMethod(methodName + "(" + string.Join(", ", argTypes) +
                                                                   ")");

        returnTypeName = Regex.Replace(returnTypeName, "\\`\\d+", "").Replace('/', '.').Replace('+', '.');
        for (var index = 0; index < argTypes.Length; index++)
        {
            var argType = argTypes[index];
            argTypes[index] = Regex.Replace(argType, "\\`\\d+", "").Replace('/', '.').Replace('+', '.');
        }

        var methodsSeen = 0;
        var lastMethod = nint.Zero;
        var iter = nint.Zero;
        nint method;
        while ((method = il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
        {
            if (il2cpp_method_get_name(method) != methodName)
                continue;

            if (il2cpp_method_get_param_count(method) != argTypes.Length)
                continue;

            if (il2cpp_method_is_generic(method) != isGeneric)
                continue;

            var returnType = il2cpp_method_get_return_type(method);
            var returnTypeNameActual = il2cpp_type_get_name(returnType);
            if (returnTypeNameActual != returnTypeName)
                continue;

            methodsSeen++;
            lastMethod = method;

            var badType = false;
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = il2cpp_method_get_param(method, (uint)i);
                var typeName = il2cpp_type_get_name(paramType);
                if (typeName != argTypes[i])
                {
                    badType = true;
                    break;
                }
            }

            if (badType) continue;

            return method;
        }

        var className = il2cpp_class_get_name(clazz);

        if (methodsSeen == 1)
        {
            Logger.Instance.LogTrace(
                "Method {ClassName}::{MethodName} was stubbed with a random matching method of the same name", className, methodName);
            Logger.Instance.LogTrace(
                "Stubby return type/target: {LastMethod} / {ReturnTypeName}", il2cpp_type_get_name(il2cpp_method_get_return_type(lastMethod)), returnTypeName);
            Logger.Instance.LogTrace("Stubby parameter types/targets follow:");
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = il2cpp_method_get_param(lastMethod, (uint)i);
                var typeName = il2cpp_type_get_name(paramType);
                Logger.Instance.LogTrace("    {TypeName} / {ArgType}", typeName, argTypes[i]);
            }

            return lastMethod;
        }

        Logger.Instance.LogTrace("Unable to find method {ClassName}::{MethodName}; signature follows", className, methodName);
        Logger.Instance.LogTrace("    return {ReturnTypeName}", returnTypeName);
        foreach (var argType in argTypes)
            Logger.Instance.LogTrace("    {ArgType}", argType);
        Logger.Instance.LogTrace("Available methods of this name follow:");
        iter = nint.Zero;
        while ((method = il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
        {
            if (il2cpp_method_get_name(method) != methodName)
                continue;

            var nParams = il2cpp_method_get_param_count(method);
            Logger.Instance.LogTrace("Method starts");
            Logger.Instance.LogTrace(
                "     return {MethodTypeName}", il2cpp_type_get_name(il2cpp_method_get_return_type(method)));
            for (var i = 0; i < nParams; i++)
            {
                var paramType = il2cpp_method_get_param(method, (uint)i);
                var typeName = il2cpp_type_get_name(paramType);
                Logger.Instance.LogTrace("    {TypeName}", typeName);
            }

            return method;
        }

        return NativeStructUtils.GetMethodInfoForMissingMethod(className + "::" + methodName + "(" +
                                                               string.Join(", ", argTypes) + ")");
    }

    public static nint GetIl2CppGenericInstanceMethod(nint methodInfoPointer, nint declaringTypeClassPointer, params nint[] genericMethodArguments)
    {
        // Ensure Il2CppSystem.RuntimeType is initialized before we call Il2CppSystem.Type.internal_from_handle
        RuntimeHelpers.RunClassConstructor(typeof(Il2CppSystem.RuntimeType).TypeHandle);

        var types = new Il2CppSystem.Type[genericMethodArguments.Length];
        for (var i = 0; i < genericMethodArguments.Length; i++)
        {
            types[i] = Il2CppSystem.Type.internal_from_handle(il2cpp_class_get_type(genericMethodArguments[i]));
        }
        var methodInfo = (Il2CppSystem.Reflection.MethodInfo)Il2CppObjectPool.Get(il2cpp_method_get_object(methodInfoPointer, declaringTypeClassPointer))!;
        return il2cpp_method_get_from_reflection(Il2CppObjectToPtrNotNull(methodInfo.MakeGenericMethod(types)));
    }

    public static nint GetIl2CppGenericInstanceType(nint typeClassPointer, params nint[] genericTypeArguments)
    {
        // Ensure Il2CppSystem.RuntimeType is initialized before we call Il2CppSystem.Type.internal_from_handle
        RuntimeHelpers.RunClassConstructor(typeof(Il2CppSystem.RuntimeType).TypeHandle);

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

    public static nint Il2CppGCHandleGetTargetOrThrow(nint gchandle)
    {
        var obj = il2cpp_gchandle_get_target(gchandle);
        if (obj == nint.Zero)
            throw new ObjectCollectedException("Object was garbage collected in IL2CPP domain");
        return obj;
    }

    public static bool Il2CppGCHandleGetTargetWasCollected(nint gchandle)
    {
        var obj = il2cpp_gchandle_get_target(gchandle);
        return obj == nint.Zero;
    }

    public static string? Il2CppStringToManaged(nint il2CppString)
    {
        if (il2CppString == nint.Zero) return null;

        var length = il2cpp_string_length(il2CppString);
        var chars = il2cpp_string_chars(il2CppString);

        return new string(chars, 0, length);
    }

    public static nint ManagedStringToIl2Cpp(string? str)
    {
        if (str == null) return nint.Zero;

        fixed (char* chars = str)
        {
            return il2cpp_string_new_utf16(chars, str.Length);
        }
    }

    public static nint Il2CppObjectToPtr(Object obj)
    {
        return obj?.Pointer ?? nint.Zero;
    }

    public static nint Il2CppObjectToPtrNotNull(Object obj)
    {
        return obj?.Pointer ?? throw new NullReferenceException();
    }

    public static nint GetIl2CppNestedType(nint enclosingType, string nestedTypeName)
    {
        if (enclosingType == nint.Zero) return nint.Zero;

        var iter = nint.Zero;
        nint nestedTypePtr;
        if (il2cpp_class_is_inflated(enclosingType))
        {
            Logger.Instance.LogTrace("Original class was inflated, falling back to reflection");

            return RuntimeReflectionHelper.GetNestedTypeViaReflection(enclosingType, nestedTypeName);
        }

        while ((nestedTypePtr = il2cpp_class_get_nested_types(enclosingType, ref iter)) != nint.Zero)
            if (il2cpp_class_get_name(nestedTypePtr) == nestedTypeName)
                return nestedTypePtr;

        Logger.Instance.LogError(
            "Nested type {NestedTypeName} on {EnclosingTypeName} not found!", nestedTypeName, il2cpp_class_get_name(enclosingType));

        return nint.Zero;
    }

    public static T ResolveICall<T>(string signature) where T : Delegate
    {
        var icallPtr = il2cpp_resolve_icall(signature);
        if (icallPtr == nint.Zero)
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

    private static T GenerateDelegateForICall<T>(nint icallPtr) where T : Delegate
    {
        var invoke = typeof(T).GetMethod("Invoke")!;

        var trampoline = new DynamicMethod("(icall delegate) " + typeof(T).FullName,
            invoke.ReturnType, invoke.GetParameters().Select(it => it.ParameterType).ToArray(), typeof(IL2CPP), true);
        var bodyBuilder = trampoline.GetILGenerator();

        var sizeOfMethod = typeof(Il2CppTypeHelper).GetMethod(nameof(Il2CppTypeHelper.SizeOf))!;
        var parameters = invoke.GetParameters();
        var parameterTypes = new Type[parameters.Length];
        var locals = new LocalBuilder[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterType = parameter.ParameterType;

            // Parameter is a ByReference<T>, and we need to get the underlying type T
            var elementType = parameterType.GenericTypeArguments[0];

            var nativeStruct = TrampolineHelpers.NativeType(elementType);
            parameterTypes[i] = nativeStruct;

            var nativeLocal = bodyBuilder.DeclareLocal(nativeStruct);
            locals[i] = nativeLocal;

            bodyBuilder.Emit(OpCodes.Ldarga, i);
            bodyBuilder.Emit(OpCodes.Ldloca, nativeLocal);
            bodyBuilder.Emit(OpCodes.Call, parameterType.GetMethod(nameof(ByReference<>.CopyToUnmanaged))!.MakeGenericMethod(nativeStruct));
        }

        foreach (var local in locals)
        {
            bodyBuilder.Emit(OpCodes.Ldloc, local);
        }

        bodyBuilder.Emit(OpCodes.Ldc_I8, icallPtr);
        bodyBuilder.Emit(OpCodes.Conv_I);

        if (invoke.ReturnType == typeof(void))
        {
            bodyBuilder.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(void), parameterTypes, []);
        }
        else
        {
            var returnType = invoke.ReturnType;
            var nativeStruct = TrampolineHelpers.NativeType(returnType);

            bodyBuilder.EmitCalli(OpCodes.Calli, CallingConventions.Standard, nativeStruct, parameterTypes, []);

            var returnLocal = bodyBuilder.DeclareLocal(nativeStruct);
            bodyBuilder.Emit(OpCodes.Stloc, returnLocal);

            bodyBuilder.Emit(OpCodes.Ldloca, returnLocal);
            bodyBuilder.Emit(OpCodes.Call, typeof(Il2CppTypeHelper).GetMethod(nameof(Il2CppTypeHelper.ReadFromPointer))!.MakeGenericMethod(returnType));
        }
        bodyBuilder.Emit(OpCodes.Ret);

        return (T)trampoline.CreateDelegate(typeof(T));
    }

    // IL2CPP Functions
    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_init(nint domain_name);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_init_utf16(nint domain_name);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_shutdown();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_config_dir(nint config_path);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_data_dir(nint data_path);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_temp_dir(nint temp_path);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_commandline_arguments(int argc, nint argv, nint basedir);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_commandline_arguments_utf16(int argc, nint argv, nint basedir);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_config_utf16(nint executablePath);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_config(nint executablePath);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_memory_callbacks(nint callbacks);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_get_corlib();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_add_internal_call(nint name, nint method);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_resolve_icall([MarshalAs(UnmanagedType.LPStr)] string name);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_alloc(uint size);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_free(nint ptr);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_array_class_get(nint element_class, uint rank);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_array_length(nint array);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_array_get_byte_length(nint array);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_array_new(nint elementTypeInfo, ulong length);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_array_new_specific(nint arrayTypeInfo, ulong length);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_array_new_full(nint array_class, ulong* lengths, ulong* lower_bounds);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_bounded_array_class_get(nint element_class, uint rank, [MarshalAs(UnmanagedType.I1)] bool bounded);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_array_element_size(nint array_class);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_assembly_get_image(nint assembly);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_enum_basetype(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_generic(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_inflated(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_assignable_from(nint klass, nint oklass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_subclass_of(nint klass, nint klassc, [MarshalAs(UnmanagedType.I1)] bool check_interfaces);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_has_parent(nint klass, nint klassc);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_from_il2cpp_type(nint type);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_from_name(nint image, [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaze, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_from_system_type(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_element_class(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_events(nint klass, ref nint iter);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_fields(nint klass, ref nint iter);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_nested_types(nint klass, ref nint iter);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_interfaces(nint klass, ref nint iter);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_properties(nint klass, ref nint iter);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_property_from_name(nint klass, nint name);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_field_from_name(nint klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_methods(nint klass, ref nint iter);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_method_from_name(nint klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int argsCount);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_class_get_name(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_class_get_namespace(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_parent(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_declaring_type(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_class_instance_size(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_class_num_fields(nint enumKlass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_valuetype(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_class_value_size(nint klass, ref uint align);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_blittable(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_class_get_flags(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_abstract(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_interface(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_class_array_element_size(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_from_type(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_type(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_class_get_type_token(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_has_attribute(nint klass, nint attr_class);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_has_references(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_class_is_enum(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_image(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_class_get_assemblyname(nint klass);

    public static string? il2cpp_class_get_assemblyname_(nint klass)
        => Marshal.PtrToStringUTF8(il2cpp_class_get_assemblyname(klass));

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_class_get_rank(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_class_get_bitmap_size(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_class_get_bitmap(nint klass, ref uint bitmap);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_stats_dump_to_file(nint path);

    //[LibraryImport("GameAssembly")]
    //[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    //public static partial ulong il2cpp_stats_get_value(IL2CPP_Stat stat);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_domain_get();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_domain_assembly_open(nint domain, nint name);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint* il2cpp_domain_get_assemblies(nint domain, ref uint size);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_exception_from_name_msg(nint image, nint name_space, nint name, nint msg);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_get_exception_argument_null(nint arg);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_format_exception(nint ex, void* message, int message_size);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_format_stack_trace(nint ex, void* output, int output_size);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_unhandled_exception(nint ex);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_field_get_flags(nint field);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_field_get_name(nint field);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_field_get_parent(nint field);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_field_get_offset(nint field);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_field_get_type(nint field);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_field_get_value(nint obj, nint field, void* value);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_field_get_value_object(nint field, nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_field_has_attribute(nint field, nint attr_class);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_field_set_value(nint obj, nint field, void* value);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_field_static_get_value(nint field, void* value);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_field_static_set_value(nint field, void* value);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_field_set_value_object(nint instance, nint field, nint value);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_gc_collect(int maxGenerations);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_gc_collect_a_little();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_gc_disable();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_gc_enable();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_gc_is_disabled();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long il2cpp_gc_get_used_size();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long il2cpp_gc_get_heap_size();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_gc_wbarrier_set_field(nint obj, nint targetAddress, nint gcObj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_gchandle_new(nint obj, [MarshalAs(UnmanagedType.I1)] bool pinned);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_gchandle_new_weakref(nint obj, [MarshalAs(UnmanagedType.I1)] bool track_resurrection);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_gchandle_get_target(nint gchandle);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_gchandle_free(nint gchandle);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_unity_liveness_calculation_begin(nint filter, int max_object_count, nint callback, nint userdata, nint onWorldStarted, nint onWorldStopped);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_unity_liveness_calculation_end(nint state);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_unity_liveness_calculation_from_root(nint root, nint state);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_unity_liveness_calculation_from_statics(nint state);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_method_get_return_type(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_method_get_declaring_type(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_method_get_name(nint method);

    public static nint il2cpp_method_get_from_reflection(nint method)
    {
        if (UnityVersionHandler.HasGetMethodFromReflection) return _il2cpp_method_get_from_reflection(method);
        Il2CppReflectionMethod* reflectionMethod = (Il2CppReflectionMethod*)method;
        return (nint)reflectionMethod->method;
    }

    [LibraryImport("GameAssembly", EntryPoint = "il2cpp_method_get_from_reflection")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint _il2cpp_method_get_from_reflection(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_method_get_object(nint method, nint refclass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_method_is_generic(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_method_is_inflated(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_method_is_instance(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_method_get_param_count(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_method_get_param(nint method, uint index);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_method_get_class(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_method_has_attribute(nint method, nint attr_class);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_method_get_flags(nint method, ref uint iflags);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_method_get_token(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_method_get_param_name(nint method, uint index);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_profiler_install(nint prof, nint shutdown_callback);

    //[LibraryImport("GameAssembly")]
    //[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    //public static partial void il2cpp_profiler_set_events(IL2CPP_ProfileFlags events);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_profiler_install_enter_leave(nint enter, nint fleave);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_profiler_install_allocation(nint callback);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_profiler_install_gc(nint callback, nint heap_resize_callback);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_profiler_install_fileio(nint callback);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_profiler_install_thread(nint start, nint end);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_property_get_flags(nint prop);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_property_get_get_method(nint prop);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_property_get_set_method(nint prop);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_property_get_name(nint prop);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_property_get_parent(nint prop);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_object_get_class(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_object_get_size(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_object_get_virtual_method(nint obj, nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_object_new(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_object_unbox(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_value_box(nint klass, nint data);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_monitor_enter(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_monitor_try_enter(nint obj, uint timeout);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_monitor_exit(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_monitor_pulse(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_monitor_pulse_all(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_monitor_wait(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_monitor_try_wait(nint obj, uint timeout);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_runtime_invoke(nint method, nint obj, void** param, ref nint exc);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // param can be of Il2CppObject*
    public static partial nint il2cpp_runtime_invoke_convert_args(nint method, nint obj, void** param, int paramCount, ref nint exc);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_runtime_class_init(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_runtime_object_init(nint obj);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_runtime_object_init_exception(nint obj, ref nint exc);

    //[LibraryImport("GameAssembly")]
    //[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    //public static partial void il2cpp_runtime_unhandled_exception_policy_set(IL2CPP_RuntimeUnhandledExceptionPolicy value);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_string_length(nint str);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial char* il2cpp_string_chars(nint str);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_string_new(string str);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_string_new_len(string str, uint length);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_string_new_utf16(char* text, int len);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_string_new_wrapper(string str);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_string_intern(string str);

    [LibraryImport("GameAssembly", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_string_is_interned(string str);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_thread_current();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_thread_attach(nint domain);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_thread_detach(nint thread);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void** il2cpp_thread_get_all_attached_threads(ref uint size);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_is_vm_thread(nint thread);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_current_thread_walk_frame_stack(nint func, nint user_data);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_thread_walk_frame_stack(nint thread, nint func, nint user_data);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_current_thread_get_top_frame(nint frame);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_thread_get_top_frame(nint thread, nint frame);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_current_thread_get_frame_at(int offset, nint frame);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_thread_get_frame_at(nint thread, int offset, nint frame);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_current_thread_get_stack_depth();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_thread_get_stack_depth(nint thread);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_type_get_object(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int il2cpp_type_get_type(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_type_get_class_or_element_class(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_type_get_name(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_type_is_byref(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_type_get_attrs(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_type_equals(nint type, nint otherType);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_type_get_assembly_qualified_name(nint type);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_image_get_assembly(nint image);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_image_get_name(nint image);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    public static partial string il2cpp_image_get_filename(nint image);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_image_get_entry_point(nint image);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint il2cpp_image_get_class_count(nint image);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_image_get_class(nint image, uint index);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_capture_memory_snapshot();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_free_captured_memory_snapshot(nint snapshot);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_set_find_plugin_callback(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_register_log_callback(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_debugger_set_agent_options(nint options);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_is_debugger_attached();

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_unity_install_unitytls_interface(void* unitytlsInterfaceStruct);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_custom_attrs_from_class(nint klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_custom_attrs_from_method(nint method);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_custom_attrs_get_attr(nint ainfo, nint attr_klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool il2cpp_custom_attrs_has_attr(nint ainfo, nint attr_klass);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint il2cpp_custom_attrs_construct(nint cinfo);

    [LibraryImport("GameAssembly")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void il2cpp_custom_attrs_free(nint ainfo);
}
