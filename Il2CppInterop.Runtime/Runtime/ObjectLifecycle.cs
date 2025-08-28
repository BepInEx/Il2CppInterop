using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;

internal static class Il2CppObjectInitializer
{
    internal static T New<T>(IntPtr ptr)
    {
        return InitializerStore<T>.Initialize(ptr);
    }

    /// <summary> Identical to <code>New</code> except skipping the glue code for injected finalizers.</summary>
    internal static T NewWithoutGlue<T>(IntPtr ptr) where T : Il2CppObjectBase
    {
        return InitializerStore<T>.InitializeWithoutGlue(ptr);
    }

    private static readonly MethodInfo _getUninitializedObject = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetUninitializedObject))!;
    private static readonly MethodInfo _getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;
    private static readonly MethodInfo _createGCHandle = typeof(Il2CppObjectBase).GetMethod(nameof(Il2CppObjectBase.CreateGCHandle), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void EmitCtorCall(ILGenerator il, Type type)
    {
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
            il.Emit(OpCodes.Call, _createGCHandle);

            var parameterlessConstructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
            if (parameterlessConstructor != null)
            {
                // obj..ctor();
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, parameterlessConstructor);
            }
        }
    }


    internal static void EmitInjectedInitialization(ILGenerator il, Type type, FieldInfo[] fieldsToInitialize)
    {
        EmitFieldInitialization(il, type, fieldsToInitialize);
        EmitGCHandling(il, type, false);
    }

    private static readonly MethodInfo _internWeak = typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.InternWeak))!;
    private static readonly MethodInfo _glue = typeof(Il2CppFinalizers).GetMethod(nameof(Il2CppFinalizers.FinalizerGlue), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void EmitGCHandling(ILGenerator il, Type type, bool useGlue)
    {
        if (useGlue && ClassInjector.IsTypeManuallyFinalizable(type))
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Call, _internWeak);
            il.Emit(OpCodes.Call, _glue);
        }
        else
        {
            il.Emit(OpCodes.Call, _internWeak);
        }
    }

    private static void EmitFieldInitialization(ILGenerator il, Type type, FieldInfo[] fieldsToInitialize)
    {
        foreach (var field in fieldsToInitialize)
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldstr, field.Name);
            il.Emit(OpCodes.Newobj, field.FieldType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Il2CppObjectBase), typeof(string) }, Array.Empty<ParameterModifier>())!
            );
            il.Emit(OpCodes.Stfld, field);
        }
    }

    private static readonly Type[] _intPtrTypeArray = { typeof(IntPtr) };

    private static class InitializerStore<T>
    {
        public delegate T Initializer(IntPtr pointer);

        public static Initializer? initialize;
        public static Initializer? initializeWithoutGlue;
        public static Initializer Initialize => initialize ??= Create(true);
        public static Initializer InitializeWithoutGlue => initializeWithoutGlue ??= Create(false);

        private static Initializer Create(bool useGlue)
        {
            var type = Il2CppClassPointerStore<T>.CreatedTypeRedirect ?? typeof(T);

            var fieldsToInitialize = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(ClassInjector.IsFieldEligible)
                .ToArray();

            string methodName = useGlue ? "Initialize" : "InitializeWithoutGlue";
            var dynamicMethod = new DynamicMethod($"{methodName}<{typeof(T).AssemblyQualifiedName}>", type, _intPtrTypeArray);
            dynamicMethod.DefineParameter(0, ParameterAttributes.None, "pointer");

            var il = dynamicMethod.GetILGenerator();
            EmitCtorCall(il, type);
            EmitFieldInitialization(il, type, fieldsToInitialize);
            il.Emit(OpCodes.Dup);
            EmitGCHandling(il, type, useGlue);
            il.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Initializer>();
        }
    }
}

// NB: Since a managed object might be garbage collected before its unmanaged counterpart is ready,
//     we need another object (this one) to handle the cleanup.
//     The objects' lifetimes are linked by the s_finalizers ephemeron on Il2CppFinalizers.
internal class FinalizerContainer
{
    public nint gcHandle;
    public IntPtr ptr;

    ~FinalizerContainer()
    {
        Il2CppObjectPool.Free(gcHandle, ptr);
    }
}

internal static class Il2CppFinalizers
{
    internal static readonly ConcurrentDictionary<IntPtr, byte> s_dying = new();
    internal static readonly ConditionalWeakTable<Il2CppObjectBase, FinalizerContainer> s_ephemeron = new();

    internal static void RunFinalizer<T>(IntPtr ptr) where T : Il2CppObjectBase
    {
        T ephemeral = Il2CppObjectInitializer.NewWithoutGlue<T>(ptr);
        Action<T> finalize = (Action<T>)ClassInjector.ManualFinalizeCache[typeof(T)];
        finalize(ephemeral);
        GC.SuppressFinalize(ephemeral);
    }

    internal static void FinalizerGlue(Il2CppObjectBase obj)
    {
        s_ephemeron.Add(obj, obj.CreateFinalizerContainer());
        obj.finalizerState = Il2CppObjectFinalizerState.Glued;
        GC.SuppressFinalize(obj);
    }
}

