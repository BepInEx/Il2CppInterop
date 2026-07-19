using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Il2CppInterop.Common;

public static class Il2CppObjectPool
{
    private static readonly ConcurrentDictionary<nint, WeakReference<object>> s_cache = new();

    private static readonly ConcurrentDictionary<nint, InitializerFunction> s_initializers = new();

    public static void Remove(nint ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "This will never be called in a trimmed context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "This will never be called in an AOT context.")]
    public static object? Get(nint ptr)
    {
        if (ptr == nint.Zero)
            return null;

        if (s_cache.TryGetValue(ptr, out var reference) && reference.TryGetTarget(out var cachedObject))
        {
            return cachedObject;
        }

        var ownClass = IL2CPP.il2cpp_object_get_class(ptr);
        if (!s_initializers.TryGetValue(ownClass, out var initializer))
        {
            var systemType = EnsureClassInitialized(ownClass);
            if (!s_initializers.TryGetValue(ownClass, out initializer))
            {
                throw new InvalidOperationException($"No object initializer found for class {systemType}");
            }
        }

        var newObj = initializer.Invoke((ObjectPointer)ptr);
        if (!newObj.GetType().IsValueType)
        {
            s_cache[ptr] = new WeakReference<object>(newObj);
        }

        return newObj;
    }

    private static void RegisterInitializer(nint classPtr, InitializerFunction initializer)
    {
        ArgumentOutOfRangeException.ThrowIfZero(classPtr);
        if (!s_initializers.TryAdd(classPtr, initializer))
        {
            var className = IL2CPP.il2cpp_class_get_name(classPtr);
            throw new InvalidOperationException($"Initializer for class {className} is already registered");
        }
    }

    internal static unsafe void RegisterInitializer<T>() where T : IIl2CppType<T>
    {
        RegisterInitializer(Il2CppType.GetClassPointer<T>(), new InitializerFunction(&TypeInitializer));

        static object TypeInitializer(ObjectPointer obj)
        {
            return T.UnboxNative(obj)!;
        }
    }

    private readonly unsafe struct InitializerFunction
    {
        private readonly delegate*<ObjectPointer, object> initializer;

        public InitializerFunction(delegate*<ObjectPointer, object> initializer)
        {
            this.initializer = initializer;
        }

        public object Invoke(ObjectPointer obj)
        {
            return initializer(obj);
        }
    }

    [RequiresDynamicCode("")]
    [RequiresUnreferencedCode("")]
    private static Type EnsureClassInitialized(nint classPointer)
    {
        ArgumentOutOfRangeException.ThrowIfZero(classPointer);
        var il2CppSystemType = Il2CppSystemTypeFromClassPointer(null, classPointer);
        var systemType = Il2CppSystemTypeToSystemType(null, il2CppSystemType);
        RuntimeHelpers.RunClassConstructor(systemType.TypeHandle);
        return systemType;

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "FromClassPointer")]
        [return: UnsafeAccessorType("Il2CppSystem.Type, Il2Cppmscorlib")]
        static extern object Il2CppSystemTypeFromClassPointer([UnsafeAccessorType("Il2CppInterop.Runtime.Extensions.Il2CppSystemTypeExtensions, Il2CppInterop.Runtime")] object? obj, nint classPointer);

        [RequiresDynamicCode("")]
        [RequiresUnreferencedCode("")]
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ToSystemType")]
        static extern Type Il2CppSystemTypeToSystemType([UnsafeAccessorType("Il2CppInterop.Runtime.Extensions.Il2CppSystemTypeExtensions, Il2CppInterop.Runtime")] object? obj, [UnsafeAccessorType("Il2CppSystem.Type, Il2Cppmscorlib")] object il2CppSystemType);
    }
}
