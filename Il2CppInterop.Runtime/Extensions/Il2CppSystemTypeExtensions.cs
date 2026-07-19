using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Reflection;

namespace Il2CppInterop.Runtime.Extensions;

internal static class Il2CppSystemTypeExtensions
{
    static Il2CppSystemTypeExtensions()
    {
        RuntimeHelpers.RunClassConstructor(typeof(String).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Type).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(RuntimeType).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(AssemblyName).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(RuntimeAssembly).TypeHandle);
    }
    extension(Type type)
    {
        public static Type FromTypePointer(nint typePointer)
        {
            return Type.internal_from_handle(typePointer);
        }

        // Note: This method is referenced via UnsafeAccessor in Il2CppObjectPool
        public static Type FromClassPointer(nint classPointer)
        {
            var il2CppType = IL2CPP.il2cpp_class_get_type(classPointer);
            if (il2CppType == default)
            {
                throw new System.ArgumentException($"Class pointer {classPointer} does not have a corresponding IL2CPP type pointer", nameof(classPointer));
            }
            return Type.FromTypePointer(il2CppType);
        }

        [RequiresDynamicCode("")]
        public static Type FromSystemType(System.Type systemType)
        {
            var classPointer = Il2CppType.GetClassPointer(systemType);
            if (classPointer == default)
            {
                throw new System.ArgumentException($"{systemType} does not have a corresponding IL2CPP class pointer", nameof(systemType));
            }

            return FromClassPointer(classPointer);
        }

        // Note: This method is referenced via UnsafeAccessor in Il2CppObjectPool
        [RequiresDynamicCode("")]
        [RequiresUnreferencedCode("")]
        public System.Type ToSystemType()
        {
            if (IsTypeDefinition(type))
            {
                return GetSystemTypeDefinition(type);
            }
            else if (type.ContainsGenericParameters)
            {
                throw new System.NotSupportedException($"Cannot convert type {type.FullName} to a system type because it contains generic parameters.");
            }
            else if (type.IsByRef)
            {
                return typeof(ByReference<>).MakeGenericType(type.GetElementType().ToSystemType());
            }
            else if (type.IsPointer)
            {
                return typeof(Pointer<>).MakeGenericType(type.GetElementType().ToSystemType());
            }
            else if (type.IsSZArray)
            {
                return typeof(Il2CppArrayRank1<>).MakeGenericType(type.GetElementType().ToSystemType());
            }
            else if (type.IsArray)
            {
                int rank = type.GetArrayRank();
                var arrayGenericType = rank switch
                {
                    1 => typeof(Il2CppArrayRank1<>),
                    2 => typeof(Il2CppArrayRank2<>),
                    3 => typeof(Il2CppArrayRank3<>),
                    4 => typeof(Il2CppArrayRank4<>),
                    5 => typeof(Il2CppArrayRank5<>),
                    _ => throw new System.NotSupportedException($"Cannot convert type {type.FullName} to a system type because it is an array with rank {rank}, which is not supported.")
                };
                return arrayGenericType.MakeGenericType(type.GetElementType().ToSystemType());
            }
            else if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition().ToSystemType();
                var genericArguments = type.GetGenericArguments().Select(t => t.ToSystemType()).ToArray();
                return genericTypeDefinition.MakeGenericType(genericArguments);
            }
            else
            {
                throw new System.NotSupportedException($"Cannot convert type {type.FullName} to a system type.");
            }
        }

        public nint ToTypePointer()
        {
            return type.TypeHandle.value;
        }

        /// <summary>
        /// Get the class pointer for this type
        /// </summary>
        /// <remarks>
        /// This can be null if the type doesn't have a corresponding class, such as generic type instance not used in the game.
        /// </remarks>
        /// <returns>The class pointer for this type</returns>
        public nint ToClassPointer()
        {
            return IL2CPP.il2cpp_class_from_type(type.ToTypePointer());
        }
    }

    [RequiresUnreferencedCode("")]
    private static System.Type GetSystemTypeDefinition(Type type)
    {
        if (type.IsNested)
        {
            var declaringType = GetSystemTypeDefinition(type.DeclaringType);
            var il2CppTypeName = type.Name ?? "";
            return TryGetNestedSystemType(declaringType, il2CppTypeName)
                ?? throw new System.NullReferenceException($"Could not find system type for nested type {il2CppTypeName} in declaring type {declaringType.FullName}");
        }
        else
        {
            return TryGetTopLevelSystemType(type.Assembly.GetName().Name ?? "", type.Namespace ?? "", type.Name ?? "")
                ?? throw new System.NullReferenceException($"Could not find system type for top-level type {type.FullName}");
        }
    }

    private static System.Type? TryGetNestedSystemType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)] System.Type declaringType, string il2CppTypeName)
    {
        foreach (var nestedType in declaringType.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (!nestedType.IsAssignableTo(typeof(IIl2CppType)))
            {
                continue;
            }

            if (string.Equals(Il2CppType.GetName(nestedType), il2CppTypeName, System.StringComparison.Ordinal))
            {
                return nestedType;
            }
        }
        return null;
    }

    [RequiresUnreferencedCode("")]
    private static System.Type? TryGetTopLevelSystemType(string il2CppAssemblyName, string il2CppTypeNamespace, string il2CppTypeName)
    {
        var systemAssembly = TryGetSystemAssembly(il2CppAssemblyName);

        if (systemAssembly == null)
        {
            return null;
        }

        foreach (var type in systemAssembly.GetTypes())
        {
            if (type.IsNested || !type.IsAssignableTo(typeof(IIl2CppType)))
            {
                continue;
            }
            if (string.Equals(Il2CppType.GetNamespace(type), il2CppTypeNamespace, System.StringComparison.Ordinal) &&
                string.Equals(Il2CppType.GetName(type), il2CppTypeName, System.StringComparison.Ordinal))
            {
                return type;
            }
        }

        return null;
    }

    private static System.Reflection.Assembly? TryGetSystemAssembly(string il2CppAssemblyName)
    {
        if (il2CppAssemblyName.StartsWith("Unity", System.StringComparison.Ordinal))
        {
            return TryGetSystemAssemblyImplementation(il2CppAssemblyName) ?? TryGetSystemAssemblyImplementation("Il2Cpp" + il2CppAssemblyName);
        }
        else
        {
            return TryGetSystemAssemblyImplementation("Il2Cpp" + il2CppAssemblyName) ?? TryGetSystemAssemblyImplementation(il2CppAssemblyName);
        }

        static System.Reflection.Assembly? TryGetSystemAssemblyImplementation(string exactName)
        {
            var alternateName = exactName.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase)
                ? exactName.Substring(0, exactName.Length - 4)
                : exactName + ".dll";

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (string.Equals(name, exactName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, alternateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return assembly;
                }
            }
            return null;
        }
    }

    private static bool IsTypeDefinition(Type type)
    {
        // IsTypeDefinition has a simple implementation and is frequently stripped, so we implement it here to avoid relying on unstripping.
        return !type.HasElementType && !type.IsConstructedGenericType && !type.IsGenericParameter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetGenericArguments")]
    [return: UnsafeAccessorType($"Il2CppInterop.Runtime.InteropTypes.Arrays.{nameof(Il2CppArrayRank1<>)}`1[[Il2CppSystem.Type, Il2Cppmscorlib]]")]
    private static extern object GetGenericArgumentsInternal(Type type);

    private static IReadOnlyList<Type> GetGenericArguments(this Type type)
    {
        return (IReadOnlyList<Type>)GetGenericArgumentsInternal(type);
    }
}
