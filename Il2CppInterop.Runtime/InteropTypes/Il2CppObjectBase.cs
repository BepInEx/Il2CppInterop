using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public class Il2CppObjectBase
{
    private static readonly MethodInfo _unboxMethod = typeof(Il2CppObjectBase).GetMethod(nameof(Unbox));
    internal bool isWrapped;
    internal IntPtr pooledPtr;

    private nint myGcHandle;

    public Il2CppObjectBase(IntPtr pointer)
    {
        CreateGCHandle(pointer);
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
    }

    public T Cast<T>() where T : Il2CppObjectBase
    {
        return TryCast<T>() ?? throw new InvalidCastException(
            $"Can't cast object of type {Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_object_get_class(Pointer)))} to type {typeof(T)}");
    }

    internal static unsafe T UnboxUnsafe<T>(IntPtr pointer)
    {
        var nestedTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (nestedTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException($"{typeof(T)} is not an Il2Cpp reference type");

        var ownClass = IL2CPP.il2cpp_object_get_class(pointer);
        if (!IL2CPP.il2cpp_class_is_assignable_from(nestedTypeClassPointer, ownClass))
            throw new InvalidCastException(
                $"Can't cast object of type {Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(ownClass))} to type {typeof(T)}");

        return Unsafe.AsRef<T>(IL2CPP.il2cpp_object_unbox(pointer).ToPointer());
    }

    public T Unbox<T>() where T : unmanaged
    {
        return UnboxUnsafe<T>(Pointer);
    }

    private static readonly Type[] _intPtrTypeArray = { typeof(IntPtr) };
    private static readonly MethodInfo _getUninitializedObject = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetUninitializedObject))!;
    private static readonly MethodInfo _getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;
    private static readonly MethodInfo _createGCHandle = typeof(Il2CppObjectBase).GetMethod(nameof(CreateGCHandle), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo _isWrapped = typeof(Il2CppObjectBase).GetField(nameof(isWrapped), BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static class InitializerStore<T>
    {
        private static Func<IntPtr, T>? _initializer;

        private static Func<IntPtr, T> Create()
        {
            var type = Il2CppClassPointerStore<T>.CreatedTypeRedirect ?? typeof(T);

            var dynamicMethod = new DynamicMethod($"Initializer<{typeof(T).AssemblyQualifiedName}>", type, _intPtrTypeArray);
            dynamicMethod.DefineParameter(0, ParameterAttributes.None, "pointer");

            var il = dynamicMethod.GetILGenerator();

            if (type.GetConstructor(new[] { typeof(IntPtr) }) is { } pointerConstructor)
            {
                // Base case: Il2Cpp constructor => call it directly
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, pointerConstructor);
            }
            else
            {
                // Special case: We have a parameterless constructor
                // However, it could be be user-made or implicit
                // In that case we set the GCHandle and then call the ctor and let GC destroy any objects created by DerivedConstructorPointer

                // var obj = (T)RuntimeHelpers.GetUninitializedObject(type);
                il.Emit(OpCodes.Ldtoken, type);
                il.Emit(OpCodes.Call, _getTypeFromHandle);
                il.Emit(OpCodes.Call, _getUninitializedObject);
                il.Emit(OpCodes.Castclass, type);

                // obj.CreateGCHandle(pointer);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, _createGCHandle);

                // obj.isWrapped = true;
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stfld, _isWrapped);

                var parameterlessConstructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
                if (parameterlessConstructor != null)
                {
                    // obj..ctor();
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Callvirt, parameterlessConstructor);
                }
            }

            il.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Func<IntPtr, T>>();
        }

        public static Func<IntPtr, T> Initializer => _initializer ??= Create();
    }

    public T? TryCast<T>() where T : Il2CppObjectBase
    {
        var nestedTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (nestedTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException($"{typeof(T)} is not an Il2Cpp reference type");

        var ownClass = IL2CPP.il2cpp_object_get_class(Pointer);
        if (!IL2CPP.il2cpp_class_is_assignable_from(nestedTypeClassPointer, ownClass))
            return null;

        if (RuntimeSpecificsStore.IsInjected(ownClass))
        {
            if (ClassInjectorBase.GetMonoObjectFromIl2CppPointer(Pointer) is T monoObject) return monoObject;
        }

        return InitializerStore<T>.Initializer(Pointer);
    }

    ~Il2CppObjectBase()
    {
        IL2CPP.il2cpp_gchandle_free(myGcHandle);

        if (pooledPtr == IntPtr.Zero) return;
        Il2CppObjectPool.Remove(pooledPtr);
    }
}
