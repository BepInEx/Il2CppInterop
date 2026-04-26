using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.CoreLib;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

public static class DelegateSupport
{
    private static readonly ConcurrentDictionary<MethodSignature, Type> ourDelegateTypes = new();

    private static readonly AssemblyBuilder AssemblyBuilder =
        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Il2CppTrampolineDelegates"), AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder ModuleBuilder =
        AssemblyBuilder.DefineDynamicModule("Il2CppTrampolineDelegates");

    private static readonly ConcurrentDictionary<MethodInfo, Delegate> NativeToManagedTrampolines = new();

    internal static Type GetOrCreateDelegateType(MethodSignature signature, MethodInfo managedMethod)
    {
        return ourDelegateTypes.GetOrAdd(signature, CreateDelegateType, managedMethod);
    }

    private static Type CreateDelegateType(MethodSignature signature, MethodInfo managedMethodInner)
    {
        var typeName = "Il2CppToManagedDelegate_" + managedMethodInner.DeclaringType + "_" + signature.GetHashCode() +
                       (signature.HasThis ? "HasThis" : "") +
                       (signature.ConstructedFromNative ? "FromNative" : "");

        var newType = ModuleBuilder.DefineType(typeName, TypeAttributes.Sealed | TypeAttributes.Public,
            typeof(MulticastDelegate));

        var ctor = newType.DefineConstructor(
            MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
            MethodAttributes.Public, CallingConventions.HasThis, new[] { typeof(object), typeof(IntPtr) });
        ctor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        var parameterOffset = signature.HasThis ? 1 : 0;
        var managedParameters = managedMethodInner.GetParameters();
        var parameterTypes = new Type[managedParameters.Length + 1 + parameterOffset];

        if (signature.HasThis)
            parameterTypes[0] = typeof(IntPtr);

        parameterTypes[parameterTypes.Length - 1] = typeof(Il2CppMethodInfo*);
        for (var i = 0; i < managedParameters.Length; i++)
            parameterTypes[i + parameterOffset] = managedParameters[i].ParameterType.NativeType();

        newType.DefineMethod("Invoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            CallingConventions.HasThis,
            managedMethodInner.ReturnType.NativeType(),
            parameterTypes).SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        newType.DefineMethod("BeginInvoke",
                MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot |
                MethodAttributes.Public,
                CallingConventions.HasThis, typeof(IAsyncResult),
                parameterTypes.Concat(new[] { typeof(AsyncCallback), typeof(object) }).ToArray())
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        newType.DefineMethod("EndInvoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            CallingConventions.HasThis,
            managedMethodInner.ReturnType.NativeType(),
            new[] { typeof(IAsyncResult) }).SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        return newType.CreateType();
    }

    private static string ExtractSignature(MethodInfo methodInfo)
    {
        var builder = new StringBuilder();
        builder.Append(methodInfo.ReturnType.FullName);
        if (!methodInfo.IsStatic)
        {
            builder.Append('_');
            builder.Append(methodInfo.DeclaringType!.FullName);
        }
        foreach (var parameterInfo in methodInfo.GetParameters())
        {
            builder.Append('_');
            builder.Append(parameterInfo.ParameterType.FullName);
        }

        return builder.ToString();
    }

    private static Delegate GetOrCreateNativeToManagedTrampoline(MethodSignature signature, MethodInfo managedMethod)
    {
        return NativeToManagedTrampolines.GetOrAdd(managedMethod, GenerateNativeToManagedTrampoline, signature);
    }

    private static Delegate GenerateNativeToManagedTrampoline(MethodInfo managedMethod, MethodSignature signature)
    {
        var returnType = managedMethod.ReturnType.NativeType();

        var managedParameters = managedMethod.GetParameters();
        var parameterTypes = new Type[managedParameters.Length + 1 + 1]; // thisptr for target, methodInfo last arg
        parameterTypes[0] = typeof(IntPtr);
        parameterTypes[managedParameters.Length + 1] = typeof(Il2CppMethodInfo*);
        for (var i = 0; i < managedParameters.Length; i++)
            parameterTypes[i + 1] = managedParameters[i].ParameterType.NativeType();

        var trampoline = new DynamicMethod("(il2cpp delegate trampoline) " + ExtractSignature(managedMethod),
            MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, returnType, parameterTypes,
            typeof(DelegateSupport), true);
        var bodyBuilder = trampoline.GetILGenerator();

        var tryLabel = bodyBuilder.BeginExceptionBlock();

        bodyBuilder.Emit(OpCodes.Ldarg_0);
        bodyBuilder.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get))!);
        bodyBuilder.Emit(OpCodes.Castclass, typeof(Il2CppToMonoDelegateReference));
        bodyBuilder.Emit(OpCodes.Callvirt, typeof(Il2CppToMonoDelegateReference).GetProperty(nameof(Il2CppToMonoDelegateReference.ReferencedDelegate))!.GetMethod!);

        for (var i = 0; i < managedParameters.Length; i++)
        {
            bodyBuilder.Emit(OpCodes.Ldarg, i + 1);
            bodyBuilder.Emit(OpCodes.Call, typeof(DelegateSupport).GetMethod(nameof(ConvertNativeArgument), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(parameterTypes[i + 1], managedParameters[i].ParameterType));
        }

        bodyBuilder.Emit(OpCodes.Call, managedMethod);

        LocalBuilder? returnLocal = null;
        if (returnType != typeof(void))
        {
            returnLocal = bodyBuilder.DeclareLocal(returnType);
            bodyBuilder.Emit(OpCodes.Call, typeof(DelegateSupport).GetMethod(nameof(ConvertReturnValue), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(managedMethod.ReturnType, returnType));
            bodyBuilder.Emit(OpCodes.Stloc, returnLocal);
        }

        bodyBuilder.BeginCatchBlock(typeof(Exception));
        bodyBuilder.Emit(OpCodes.Call, typeof(DelegateSupport).GetMethod(nameof(LogError), BindingFlags.Static | BindingFlags.NonPublic)!);

        bodyBuilder.EndExceptionBlock();

        if (returnLocal != null)
            bodyBuilder.Emit(OpCodes.Ldloc, returnLocal);
        bodyBuilder.Emit(OpCodes.Ret);

        return trampoline.CreateDelegate(GetOrCreateDelegateType(signature, managedMethod));
    }

    private static void LogError(Exception exception)
    {
        Logger.Instance.LogError("Exception in IL2CPP-to-Managed trampoline, not passing it to il2cpp: {Exception}", exception);
    }

    private static unsafe TManaged? ConvertNativeArgument<TNative, TManaged>(TNative value)
        where TNative : unmanaged
        where TManaged : IIl2CppType<TManaged>
    {
        var span = new ReadOnlySpan<byte>(&value, sizeof(TNative));
        return TManaged.ReadFromSpan(span);
    }

    private static unsafe TNative ConvertReturnValue<TManaged, TNative>(TManaged? value)
        where TNative : unmanaged
        where TManaged : IIl2CppType<TManaged>
    {
        TNative result = default;
        var span = new Span<byte>(&result, sizeof(TNative));
        TManaged.WriteToSpan(value, span);
        return result;
    }

    public static TIl2Cpp? ConvertDelegate<TIl2Cpp>(Delegate @delegate) where TIl2Cpp : Il2CppSystem.Delegate
    {
        if (@delegate == null)
            return null;

        if (!typeof(Il2CppSystem.Delegate).IsAssignableFrom(typeof(TIl2Cpp)))
            throw new ArgumentException($"{typeof(TIl2Cpp)} is not a delegate");

        var managedInvokeMethod = @delegate.GetType().GetMethod("Invoke")!;
        var parameterInfos = managedInvokeMethod.GetParameters();
        foreach (var parameterInfo in parameterInfos)
        {
            var parameterType = parameterInfo.ParameterType;
            if (parameterType.IsGenericParameter)
                throw new ArgumentException(
                    $"Delegate has unsubstituted generic parameter ({parameterType}) which is not supported");
        }

        var classTypePtr = Il2CppClassPointerStore<TIl2Cpp>.NativeClassPointer;
        if (classTypePtr == IntPtr.Zero)
            throw new ArgumentException($"Type {typeof(TIl2Cpp)} has uninitialized class pointer");

        var il2CppDelegateType = Il2CppSystem.Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(classTypePtr));
        var nativeDelegateInvokeMethod = il2CppDelegateType.GetMethod("Invoke");

        var nativeParameters = nativeDelegateInvokeMethod.GetParameters();
        if (nativeParameters.Count != parameterInfos.Length)
            throw new ArgumentException(
                $"Managed delegate has {parameterInfos.Length} parameters, native has {nativeParameters.Count}, these should match");

        for (var i = 0; i < nativeParameters.Count; i++)
        {
            var nativeType = nativeParameters[i].ParameterType;
            var managedType = parameterInfos[i].ParameterType;

            var classPointerFromManagedType = Il2CppClassPointerStore.GetNativeClassPointer(managedType);

            var classPointerFromNativeType = IL2CPP.il2cpp_class_from_type(nativeType._impl.value);

            if (classPointerFromManagedType != classPointerFromNativeType)
                throw new ArgumentException(
                    $"Parameter type at {i} has mismatched native type pointers; types: {nativeType.FullName} != {managedType.FullName}");
        }

        var signature = new MethodSignature(nativeDelegateInvokeMethod, true);
        var managedTrampoline = GetOrCreateNativeToManagedTrampoline(signature, managedInvokeMethod);

        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.MethodPointer = Marshal.GetFunctionPointerForDelegate(managedTrampoline);
        methodInfo.ParametersCount = (byte)parameterInfos.Length;
        methodInfo.Slot = ushort.MaxValue;
        methodInfo.IsMarshalledFromNative = true;

        var delegateReference = new Il2CppToMonoDelegateReference(@delegate, methodInfo.Pointer);

        TIl2Cpp converted = (TIl2Cpp)Activator.CreateInstance(typeof(TIl2Cpp), delegateReference, methodInfo.Pointer)!;

        converted.method_ptr = methodInfo.MethodPointer;
        converted.method_info = nativeDelegateInvokeMethod; // todo: is this truly a good hack?
        converted.method = methodInfo.Pointer;
        converted.m_target = delegateReference;

        if (UnityVersionHandler.MustUseDelegateConstructor)
        {
            // U2021.2.0+ hack in case the constructor did the wrong thing anyway
            converted.invoke_impl = converted.method_ptr;
            converted.method_code = delegateReference.Pointer;
        }

        return converted;
    }

    internal sealed class MethodSignature : IEquatable<MethodSignature>
    {
        public readonly bool ConstructedFromNative;
        public readonly bool HasThis;
        private readonly int _hashCode;

        public MethodSignature(Il2CppSystem.Reflection.MethodInfo methodInfo, bool hasThis)
        {
            HasThis = hasThis;
            ConstructedFromNative = true;

            var hashCode = new HashCode();

            hashCode.Add(methodInfo.ReturnType.GetHashCode());
            if (hasThis) hashCode.Add(methodInfo.DeclaringType.GetHashCode());
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                hashCode.Add(parameterInfo.ParameterType.GetHashCode());
            }

            _hashCode = hashCode.ToHashCode();
        }

        public MethodSignature(MethodInfo methodInfo, bool hasThis)
        {
            HasThis = hasThis;
            ConstructedFromNative = false;

            var hashCode = new HashCode();

            hashCode.Add(methodInfo.ReturnType.NativeType());
            if (hasThis) hashCode.Add(methodInfo.DeclaringType!.NativeType());
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                hashCode.Add(parameterInfo.ParameterType.NativeType());
            }

            _hashCode = hashCode.ToHashCode();
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public bool Equals(MethodSignature? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return _hashCode.GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MethodSignature);
        }

        public static bool operator ==(MethodSignature? left, MethodSignature? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MethodSignature? left, MethodSignature? right)
        {
            return !Equals(left, right);
        }
    }
}
