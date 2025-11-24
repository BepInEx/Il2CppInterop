using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public unsafe struct Pointer<T>(void* pointer) : IIl2CppType<Pointer<T>>
    where T : IIl2CppType<T>
{
    static Pointer()
    {
        // Ensure Il2CppSystem.RuntimeType is initialized before we call Il2CppSystem.Type.internal_from_handle
        RuntimeHelpers.RunClassConstructor(typeof(Il2CppSystem.RuntimeType).TypeHandle);

        var elementClassPtr = Il2CppClassPointerStore<T>.NativeClassPtr;
        ThrowHelper.ThrowIfNull(elementClassPtr);
        var elementTypePtr = IL2CPP.il2cpp_class_get_type(elementClassPtr);
        ThrowHelper.ThrowIfNull(elementTypePtr);
        var elementTypeObj = Il2CppSystem.Type.internal_from_handle(elementTypePtr);
        var pointerTypeObj = elementTypeObj.MakePointerType();
        var pointerTypePtr = pointerTypeObj.TypeHandle.value;
        ThrowHelper.ThrowIfNull(pointerTypePtr);
        var pointerClassPtr = IL2CPP.il2cpp_class_from_type(pointerTypePtr);
        ThrowHelper.ThrowIfNull(pointerClassPtr);
        Il2CppClassPointerStore<Pointer<T>>.NativeClassPtr = pointerClassPtr;
        Il2CppObjectPool.RegisterValueTypeInitializer<Pointer<T>>();
    }

    private readonly void* _pointer = pointer;

    public readonly bool IsNull => _pointer is null;

    public readonly T? this[int index]
    {
        get
        {
            ThrowIfNull();
            void* start = (byte*)_pointer + T.Size * index;
            return Il2CppTypeHelper.ReadFromPointer<T>(start);
        }
        set
        {
            ThrowIfNull();
            void* start = (byte*)_pointer + T.Size * index;
            Il2CppTypeHelper.WriteToPointer(value, start);
        }
    }

    public readonly void* ToPointer() => _pointer;

    private readonly void ThrowIfNull()
    {
        if (_pointer is null)
        {
            throw new NullReferenceException($"Cannot access reference of type {typeof(T).Name} because it is null.");
        }
    }

    static int IIl2CppType<Pointer<T>>.Size => IntPtr.Size;

    readonly nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<Pointer<T>>.NativeClassPtr;

    static Pointer<T> IIl2CppType<Pointer<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => (Pointer<T>)(void*)Il2CppTypeHelper.ReadPointer(span);
    static void IIl2CppType<Pointer<T>>.WriteToSpan(Pointer<T> value, Span<byte> span) => Il2CppTypeHelper.WritePointer((IntPtr)value._pointer, span);

    public static explicit operator Pointer<T>(void* value) => new(value);
    public static explicit operator void*(Pointer<T> pointer) => pointer._pointer;
}
