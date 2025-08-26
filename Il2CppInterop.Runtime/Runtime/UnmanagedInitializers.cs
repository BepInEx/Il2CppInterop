using System;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Runtime;

public static class InitializerStore
{
    public static Func<IntPtr, object> GetUnmanagedInitializer<T>() where T : unmanaged
    {
        return UnmanagedInitializer<T>.Initializer;
    }

    private static class UnmanagedInitializer<T> where T : unmanaged
    {
        public static Func<IntPtr, object> Initializer { get; } = Initialize;

        private static unsafe object Initialize(IntPtr ptr)
        {
            return *(T*)IL2CPP.il2cpp_object_unbox(ptr);
        }
    }

    public static Func<IntPtr, object> GetNonBlittableInitializer<T>() where T : struct, IIl2CppType<T>
    {
        return NonBlittableInitializer<T>.Initializer;
    }

    private static class NonBlittableInitializer<T> where T : struct, IIl2CppType<T>
    {
        public static Func<IntPtr, object> Initializer { get; } = Initialize;

        private static unsafe object Initialize(IntPtr ptr)
        {
            var unboxedPointer = IL2CPP.il2cpp_object_unbox(ptr);
            return Il2CppTypeHelper.ReadFromPointer<T>(unboxedPointer);
        }
    }

    public static Func<IntPtr, object> GetReferenceInitializer<T>() where T : IIl2CppObjectBase<T>
    {
        return ReferenceInitializer<T>.Initializer;
    }

    private static class ReferenceInitializer<T> where T : IIl2CppObjectBase<T>
    {
        public static Func<IntPtr, object> Initializer { get; } = Initialize;
        private static unsafe object Initialize(IntPtr ptr)
        {
            return T.Create(ptr);
        }
    }
}
