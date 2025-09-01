using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Runtime;

internal static class Il2CppObjectInitializer
{
    internal static T New<T>(IntPtr ptr)
    {
        return InitializerStore<T>.Initialize(ptr);
    }

    /// <summary>
    /// Creates a proxy to a new <c>T</c> without holding a strong handle.
    /// </summary>
    internal static T NewWeak<T>(IntPtr ptr) where T : Il2CppObjectBase
    {
        return InitializerStore<T>.InitializeWeak(ptr);
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

    private static readonly MethodInfo _intern = typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Intern))!;
    private static readonly MethodInfo _downgrade = typeof(Il2CppObjectBase).GetMethod(nameof(Il2CppObjectBase.Downgrade), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void EmitGCHandling(ILGenerator il, Type type, bool weak)
    {
        if (!weak)
        {
            il.Emit(OpCodes.Call, _intern);
        }
        else
        {
            il.Emit(OpCodes.Call, _downgrade);
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
        public static Initializer Initialize => initialize ??= Create(false);
        public static Initializer InitializeWeak => initializeWithoutGlue ??= Create(true);

        private static Initializer Create(bool weak)
        {
            var type = Il2CppClassPointerStore<T>.CreatedTypeRedirect ?? typeof(T);

            var fieldsToInitialize = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(ClassInjector.IsFieldEligible)
                .ToArray();

            string methodName = weak ? "InitializeWeak" : "Initialize";
            var dynamicMethod = new DynamicMethod($"{methodName}<{typeof(T).AssemblyQualifiedName}>", type, _intPtrTypeArray);
            dynamicMethod.DefineParameter(0, ParameterAttributes.None, "pointer");

            var il = dynamicMethod.GetILGenerator();
            EmitCtorCall(il, type);
            EmitFieldInitialization(il, type, fieldsToInitialize);
            il.Emit(OpCodes.Dup);
            EmitGCHandling(il, type, weak);
            il.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Initializer>();
        }
    }
}
