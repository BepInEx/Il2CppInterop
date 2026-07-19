using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Exceptions;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Structs;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

/// <summary>
/// Do not reference this class. Everything in it is an implementation detail of the generator. Breaking changes may occur at any time without warning.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class GenerationInternals
{
    public static unsafe string? Il2CppStringToManaged(Il2CppSystem.String? il2CppString)
    {
        if (il2CppString == null)
            return null;

        var il2CppStringPtr = il2CppString.Pointer;

        var length = IL2CPP.il2cpp_string_length(il2CppStringPtr);
        var chars = IL2CPP.il2cpp_string_chars(il2CppStringPtr);

        return new string(chars, 0, length);
    }

    public static unsafe Il2CppSystem.String? ManagedStringToIl2Cpp(string? str)
    {
        if (str == null)
            return null;

        fixed (char* chars = str)
        {
            return new Il2CppSystem.String((ObjectPointer)IL2CPP.il2cpp_string_new_utf16(chars, str.Length));
        }
    }

    [RequiresDynamicCode("")]
    public static Il2CppSystem.Type ManagedTypeToIl2CppType(Type type)
    {
        return Il2CppSystem.Type.FromSystemType(type);
    }

    [RequiresDynamicCode("")]
    [RequiresUnreferencedCode("")]
    public static Type Il2CppTypeToManagedType(Il2CppSystem.Type type)
    {
        return type.ToSystemType();
    }

    [RequiresDynamicCode("")]
    public static unsafe Il2CppSystem.TypedReference MakeRefAny<T>(void* value) where T : IIl2CppType<T>
    {
        Il2CppSystem.TypedReference result = default;
        result.Value = (IntPtr)value;
        result.Type = Il2CppTypePointerStore<T>.NativeTypePointer;
        result.type = Il2CppSystem.Type.FromTypePointer(result.Type).TypeHandle;
        return result;
    }

    public static unsafe T? RefAnyValue<T>(Il2CppSystem.TypedReference typedReference) where T : IIl2CppType<T>
    {
        return Il2CppType.ReadFromPointer<T>((void*)(nint)typedReference.Value);
    }

    public static Il2CppSystem.RuntimeTypeHandle RefAnyType(Il2CppSystem.TypedReference typedReference)
    {
        if (typedReference.type.value != nint.Zero)
            return typedReference.type;

        if (typedReference.Type != nint.Zero)
            return Il2CppSystem.Type.FromTypePointer(typedReference.Type).TypeHandle;

        throw new InvalidOperationException("TypedReference does not have a type");
    }

    public static unsafe IIl2CppType? LoadIndirectReference(void* address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var objectPointer = *(nint*)address;
        return (IIl2CppType?)Il2CppObjectPool.Get(objectPointer);
    }

    public static unsafe void StoreIndirectReference(void* address, IIl2CppType? obj)
    {
        ArgumentNullException.ThrowIfNull(address);
        var objectPointer = obj?.BoxNative() ?? ObjectPointer.Null;
        *(nint*)address = (nint)objectPointer;
    }

    // For unstripping the box instruction
    public static object? Box<T>(T? value) where T : IIl2CppType<T>
    {
        return value?.Box();
    }

    // For unstripping the unbox.any instruction
    public static T? Unbox<T>(object? obj) where T : IIl2CppType<T>
    {
        return T.Unbox(obj);
    }

    public static object? BoxNullableValueType<T>(in Il2CppSystem.Nullable<T> nullable) where T : struct, IIl2CppType<T>, Il2CppSystem.IValueType
    {
        return nullable.hasValue
            ? nullable.value.Box()
            : null;
    }

    public static Il2CppSystem.Nullable<T> UnboxNullableValueType<T>(object? obj) where T : struct, IIl2CppType<T>, Il2CppSystem.IValueType
    {
        return obj switch
        {
            null => default,
            T value => Constructor(true, value),
            Il2CppSystem.Nullable<T> nullable => nullable,
            _ => throw new InvalidCastException(),
        };

        static Il2CppSystem.Nullable<T> Constructor(bool hasValue, T value)
        {
            Il2CppSystem.Nullable<T> result = default;
            result.hasValue = hasValue;
            result.value = value;
            return result;
        }
    }

    public static nint Il2CppGCHandleGetTargetOrThrow(nint gchandle)
    {
        var obj = IL2CPP.il2cpp_gchandle_get_target(gchandle);
        if (obj == nint.Zero)
            throw new ObjectCollectedException("Object was garbage collected in IL2CPP domain");
        return obj;
    }

    public static bool Il2CppGCHandleGetTargetWasCollected(nint gchandle)
    {
        var obj = IL2CPP.il2cpp_gchandle_get_target(gchandle);
        return obj == nint.Zero;
    }

    /// <summary>
    /// Get the class pointer for a generic instance type
    /// </summary>
    /// <param name="typeClassPointer">The class pointer for the generic type definition</param>
    /// <param name="genericTypeArguments">The class pointers for the generic type arguments</param>
    /// <returns>The class pointer for the generic instance type</returns>
    public static nint GetIl2CppGenericInstanceType(nint typeClassPointer, params nint[] genericTypeArguments)
    {
        var types = new Il2CppSystem.Type[genericTypeArguments.Length];
        for (var i = 0; i < genericTypeArguments.Length; i++)
        {
            types[i] = Il2CppSystem.Type.FromClassPointer(genericTypeArguments[i]);
        }
        var genericTypeInstance = Il2CppSystem.Type.FromClassPointer(typeClassPointer).MakeGenericType(types);
        var result = genericTypeInstance.ToClassPointer();
        if (result == nint.Zero)
        {
            var className = IL2CPP.il2cpp_class_get_name(typeClassPointer);
            Logger.Instance.LogTrace("Could not get class pointer for generic instance type {ClassName}`{TypeArgumentCount}. Creating one...", className, genericTypeArguments.Length);
            result = GenericTypeInflater.InflateGenericType(typeClassPointer, genericTypeArguments);
        }
        return result;
    }

    public static nint GetIl2CppMethodByToken(nint clazz, int token)
    {
        if (clazz == nint.Zero)
            return GetMethodInfoForMissingMethod(token.ToString());

        var iter = nint.Zero;
        nint method;
        while ((method = IL2CPP.il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
            if (IL2CPP.il2cpp_method_get_token(method) == token)
                return method;

        var className = IL2CPP.il2cpp_class_get_name(clazz);
        Logger.Instance.LogTrace("Unable to find method {ClassName}::{Token}", className, token);

        return GetMethodInfoForMissingMethod($"{className}::{token}");
    }

    public static nint GetIl2CppMethod(nint clazz, bool isGeneric, string methodName, string returnTypeName, params string[] argTypes)
    {
        if (clazz == nint.Zero)
            return GetMethodInfoForMissingMethod($"{methodName}({string.Join(", ", argTypes)})");

        returnTypeName = GenericArityRegex.Replace(returnTypeName, "").Replace('/', '.').Replace('+', '.');
        for (var index = 0; index < argTypes.Length; index++)
        {
            var argType = argTypes[index];
            argTypes[index] = GenericArityRegex.Replace(argType, "").Replace('/', '.').Replace('+', '.');
        }

        var methodsSeen = 0;
        var lastMethod = nint.Zero;
        var iter = nint.Zero;
        nint method;
        while ((method = IL2CPP.il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
        {
            if (IL2CPP.il2cpp_method_get_name(method) != methodName)
                continue;

            if (IL2CPP.il2cpp_method_get_param_count(method) != argTypes.Length)
                continue;

            if (IL2CPP.il2cpp_method_is_generic(method) != isGeneric)
                continue;

            var returnType = IL2CPP.il2cpp_method_get_return_type(method);
            var returnTypeNameActual = IL2CPP.il2cpp_type_get_name(returnType);
            if (returnTypeNameActual != returnTypeName)
                continue;

            methodsSeen++;
            lastMethod = method;

            var badType = false;
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = IL2CPP.il2cpp_method_get_param(method, (uint)i);
                var typeName = IL2CPP.il2cpp_type_get_name(paramType);
                if (typeName != argTypes[i])
                {
                    badType = true;
                    break;
                }
            }

            if (badType) continue;

            return method;
        }

        var className = IL2CPP.il2cpp_class_get_name(clazz);

        if (methodsSeen == 1)
        {
            Logger.Instance.LogTrace(
                "Method {ClassName}::{MethodName} was stubbed with a random matching method of the same name", className, methodName);
            Logger.Instance.LogTrace(
                "Stubby return type/target: {LastMethod} / {ReturnTypeName}", IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_return_type(lastMethod)), returnTypeName);
            Logger.Instance.LogTrace("Stubby parameter types/targets follow:");
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = IL2CPP.il2cpp_method_get_param(lastMethod, (uint)i);
                var typeName = IL2CPP.il2cpp_type_get_name(paramType);
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
        while ((method = IL2CPP.il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
        {
            if (IL2CPP.il2cpp_method_get_name(method) != methodName)
                continue;

            var nParams = IL2CPP.il2cpp_method_get_param_count(method);
            Logger.Instance.LogTrace("Method starts");
            Logger.Instance.LogTrace("     return {MethodTypeName}", IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_return_type(method)));
            for (var i = 0; i < nParams; i++)
            {
                var paramType = IL2CPP.il2cpp_method_get_param(method, (uint)i);
                var typeName = IL2CPP.il2cpp_type_get_name(paramType);
                Logger.Instance.LogTrace("    {TypeName}", typeName);
            }

            return method;
        }

        return GetMethodInfoForMissingMethod($"{className}::{methodName}({string.Join(", ", argTypes)})");
    }

    private static IntPtr GetMethodInfoForMissingMethod(string methodName)
    {
        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.Name = Marshal.StringToCoTaskMemUTF8(methodName);
        methodInfo.Slot = ushort.MaxValue;
        return methodInfo.Pointer;
    }
    public static void Il2CppRuntimeClassInit(nint @class)
    {
        try
        {
            if (@class != nint.Zero)
            {
                IL2CPP.il2cpp_runtime_class_init(@class);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError(ex, "Error initializing class {ClassName}", IL2CPP.il2cpp_class_get_name(@class));
        }
    }

    public static nint GetIl2CppClass(string assemblyName, string namespaze, string className)
    {
        return IL2CPP.il2cpp_class_from_name(AssemblyInjector.GetOrCreateImage(assemblyName).Pointer, namespaze, className);
    }

    public static int GetIl2CppValueSize(nint klass)
    {
        if (klass == nint.Zero)
            return 0;
        return IL2CPP.il2cpp_class_value_size(klass, out _);
    }

    public static nint GetIl2CppGenericInstanceMethod(nint methodInfoPointer, nint declaringTypeClassPointer, params nint[] genericMethodArguments)
    {
        var types = new Il2CppSystem.Type[genericMethodArguments.Length];
        for (var i = 0; i < genericMethodArguments.Length; i++)
        {
            types[i] = Il2CppSystem.Type.FromClassPointer(genericMethodArguments[i]);
        }
        var methodInfoObject = (Il2CppSystem.Reflection.MethodInfo)Il2CppObjectPool.Get(IL2CPP.il2cpp_method_get_object(methodInfoPointer, declaringTypeClassPointer))!;
        var methodInfoGeneric = methodInfoObject.MakeGenericMethod(types);
        return il2cpp_method_get_from_reflection(methodInfoGeneric?.Pointer ?? throw new NullReferenceException());

        static unsafe nint il2cpp_method_get_from_reflection(nint method)
        {
            if (UnityVersionHandler.HasGetMethodFromReflection)
                return IL2CPP.il2cpp_method_get_from_reflection(method);
            Il2CppReflectionMethod* reflectionMethod = (Il2CppReflectionMethod*)method;
            return (nint)reflectionMethod->method;
        }
    }

    public static nint GetIl2CppNestedType(nint enclosingType, string nestedTypeName)
    {
        if (enclosingType == nint.Zero)
            return nint.Zero;

        var iter = nint.Zero;
        nint nestedTypePtr;
        if (IL2CPP.il2cpp_class_is_inflated(enclosingType))
        {
            Logger.Instance.LogTrace("Original class was inflated, falling back to reflection");

            return GetNestedTypeViaReflection(enclosingType, nestedTypeName);
        }

        while ((nestedTypePtr = IL2CPP.il2cpp_class_get_nested_types(enclosingType, ref iter)) != nint.Zero)
            if (IL2CPP.il2cpp_class_get_name(nestedTypePtr) == nestedTypeName)
                return nestedTypePtr;

        Logger.Instance.LogError(
            "Nested type {NestedTypeName} on {EnclosingTypeName} not found!", nestedTypeName, IL2CPP.il2cpp_class_get_name(enclosingType));

        return nint.Zero;

        static IntPtr GetNestedTypeViaReflection(nint enclosingClass, string nestedTypeName)
        {
            var reflectionType = Il2CppSystem.Type.FromClassPointer(enclosingClass);
            var nestedType = reflectionType.GetNestedType(nestedTypeName, Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic);

            return nestedType?.ToClassPointer() ?? nint.Zero;
        }
    }

    [GeneratedRegex(@"\`\d+")]
    private static partial Regex GenericArityRegex { get; }
}
