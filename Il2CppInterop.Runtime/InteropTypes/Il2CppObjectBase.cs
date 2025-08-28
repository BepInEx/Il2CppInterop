using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.InteropTypes;

public enum Il2CppObjectFinalizerState
{
    Free = 0,
    Glued = 1,
}

public class Il2CppObjectBase
{
    internal bool isWrapped;
    internal IntPtr pooledPtr;
    internal Il2CppObjectFinalizerState finalizerState;

    private bool wasDestroyed;
    private nint myGcHandle;

    public Il2CppObjectBase(IntPtr pointer)
    {
        CreateGCHandle(pointer);
    }

    ~Il2CppObjectBase()
    {
        switch (finalizerState)
        {
            case Il2CppObjectFinalizerState.Free:
                Il2CppObjectPool.Free(myGcHandle, pooledPtr);
                break;
            case Il2CppObjectFinalizerState.Glued:
                throw new NotSupportedException("Object was garbage collected too early. Perhaps GC.ReRegisterForFinalize was incorrectly called?");
        }
    }

    public IntPtr ObjectClass => IL2CPP.il2cpp_object_get_class(Pointer);

    public IntPtr Pointer
    {
        get
        {
            var handleTarget = IL2CPP.il2cpp_gchandle_get_target(myGcHandle);
            if (handleTarget == IntPtr.Zero)
                throw new ObjectCollectedException("Object was garbage collected in IL2CPP domain");
            return handleTarget;
        }
    }

    public bool WasCollected
    {
        get
        {
            var handleTarget = IL2CPP.il2cpp_gchandle_get_target(myGcHandle);
            if (handleTarget == IntPtr.Zero) return true;
            return false;
        }
    }

    internal void CreateGCHandle(IntPtr objHdl)
    {
        if (objHdl == IntPtr.Zero)
            throw new NullReferenceException();

        // This object already wraps an Il2Cpp object, skip the pointer and let it be GC'd
        if (isWrapped)
            return;

        myGcHandle = IL2CPP.il2cpp_gchandle_new(objHdl, false);
        isWrapped = true;
    }

    internal FinalizerContainer CreateFinalizerContainer()
    {
        return new FinalizerContainer()
        {
            gcHandle = myGcHandle,
            ptr = Pointer,
        };
    }

    public T Cast<T>() where T : Il2CppObjectBase
    {
        return TryCast<T>() ?? throw new InvalidCastException(
            $"Can't cast object of type {IL2CPP.il2cpp_class_get_name_(IL2CPP.il2cpp_object_get_class(Pointer))} to type {typeof(T)}");
    }

    internal static unsafe T UnboxUnsafe<T>(IntPtr pointer)
    {
        var nestedTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (nestedTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException($"{typeof(T)} is not an Il2Cpp reference type");

        var ownClass = IL2CPP.il2cpp_object_get_class(pointer);
        if (!IL2CPP.il2cpp_class_is_assignable_from(nestedTypeClassPointer, ownClass))
            throw new InvalidCastException(
                $"Can't cast object of type {IL2CPP.il2cpp_class_get_name_(ownClass)} to type {typeof(T)}");

        return Unsafe.AsRef<T>(IL2CPP.il2cpp_object_unbox(pointer).ToPointer());
    }

    public T Unbox<T>() where T : unmanaged
    {
        return UnboxUnsafe<T>(Pointer);
    }

    public T? TryCast<T>() where T : Il2CppObjectBase
    {
        var nestedTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (nestedTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException($"{typeof(T)} is not an Il2Cpp reference type");

        var ownClass = IL2CPP.il2cpp_object_get_class(Pointer);
        if (!IL2CPP.il2cpp_class_is_assignable_from(nestedTypeClassPointer, ownClass))
            return null;

        return Il2CppObjectInitializer.New<T>(Pointer);
    }
}
