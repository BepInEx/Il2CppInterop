using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public class Delegate : Object
{
    public Delegate(nint ptr) : base(ptr)
    {
    }

    public IntPtr method_ptr { get; set; }

    public IntPtr invoke_impl { get; set; }

    public object m_target { get; set; }

    public IntPtr method { get; set; }

    public IntPtr delegate_trampoline { get; set; }

    public IntPtr extra_arg { get; set; }

    public IntPtr method_code { get; set; }

    public IntPtr interp_method { get; set; }

    public IntPtr interp_invoke_impl { get; set; }

    public MethodInfo method_info { get; set; }

    public MethodInfo original_method_info { get; set; }

    public Boolean method_is_virtual { get; set; }
}
