using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public class Delegate : Object
{
    public Delegate(System.IntPtr ptr) : base(ptr)
    {
    }

    public System.IntPtr method_ptr { get; set; }

    public System.IntPtr invoke_impl { get; set; }

    public object m_target { get; set; }

    public System.IntPtr method { get; set; }

    public System.IntPtr delegate_trampoline { get; set; }

    public System.IntPtr extra_arg { get; set; }

    public System.IntPtr method_code { get; set; }

    public System.IntPtr interp_method { get; set; }

    public System.IntPtr interp_invoke_impl { get; set; }

    public MethodInfo method_info { get; set; }

    public MethodInfo original_method_info { get; set; }

    public bool method_is_virtual { get; set; }
}
