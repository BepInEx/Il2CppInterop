using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

internal sealed class Il2CppToMonoDelegateReference : Object, IIl2CppType<Il2CppToMonoDelegateReference>
{
    [Il2CppField]
    public Il2CppSystem.IntPtr MethodInfo
    {
        get => FieldAccess.GetInstanceFieldValue<Il2CppSystem.IntPtr>(this, MethodInfoFieldOffset);
        set => FieldAccess.SetInstanceFieldValue(this, MethodInfoFieldOffset, value);
    }
    [Il2CppField]
    private Il2CppSystem.IntPtr DelegateHandleValue
    {
        get => FieldAccess.GetInstanceFieldValue<Il2CppSystem.IntPtr>(this, DelegateHandleFieldOffset);
        set => FieldAccess.SetInstanceFieldValue(this, DelegateHandleFieldOffset, value);
    }
    private GCHandle<Delegate> DelegateHandle
    {
        get => GCHandle<Delegate>.FromIntPtr(DelegateHandleValue);
        set => DelegateHandleValue = GCHandle<Delegate>.ToIntPtr(value);
    }
    public Delegate ReferencedDelegate
    {
        get => DelegateHandle.Target;
        set
        {
            DelegateHandle.Dispose();
            DelegateHandle = value is not null ? new(value) : default;
        }
    }

    public Il2CppToMonoDelegateReference(ObjectPointer obj0) : base(obj0)
    {
    }

    public Il2CppToMonoDelegateReference(Delegate referencedDelegate, IntPtr methodInfo) : this(IL2CPP.NewObjectPointer<Il2CppToMonoDelegateReference>())
    {
        ReferencedDelegate = referencedDelegate;
        MethodInfo = methodInfo;
    }

    [Il2CppMethod(Name = "Finalize")]
    public override void Il2CppFinalize()
    {
        // This disposal happens when the object is collected by the Il2Cpp GC instead of the managed GC.
        // That ensures that the delegate is kept alive as long as the Il2Cpp object is alive, even if the managed wrapper gets collected.
        // In theory, the managed wrapper could be collected and recreated multiple times during the lifetime of the Il2Cpp object,
        // so this ensures that the managed fields are not disposed prematurely.
        try
        {
            Marshal.FreeHGlobal(MethodInfo);
            MethodInfo = IntPtr.Zero;
            ReferencedDelegate = null!;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"Exception in {nameof(Il2CppToMonoDelegateReference)}.{nameof(Il2CppFinalize)}: {{Exception}}", ex);
        }
        finally
        {
            base.Il2CppFinalize(); // Must call base method
        }
    }

    static int IIl2CppType<Il2CppToMonoDelegateReference>.Size => nint.Size;

    nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer;

    static Il2CppToMonoDelegateReference? IIl2CppType<Il2CppToMonoDelegateReference>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        return Il2CppType.ReadReference<Il2CppToMonoDelegateReference>(span);
    }

    static void IIl2CppType<Il2CppToMonoDelegateReference>.WriteToSpan(Il2CppToMonoDelegateReference? value, Span<byte> span)
    {
        Il2CppType.WriteReference(value, span);
    }

    static string IIl2CppType<Il2CppToMonoDelegateReference>.AssemblyName => "Assembly-CSharp";

    static readonly int MethodInfoFieldOffset;
    static readonly int DelegateHandleFieldOffset;

    static Il2CppToMonoDelegateReference()
    {
        TypeInjector.RegisterTypeInIl2Cpp<Il2CppToMonoDelegateReference>();
        MethodInfoFieldOffset = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer, nameof(MethodInfo)));
        DelegateHandleFieldOffset = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer, nameof(DelegateHandleValue)));
        Il2CppObjectPool.RegisterInitializer(Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer, ptr => new Il2CppToMonoDelegateReference(ptr));
    }
}
