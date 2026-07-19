using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Structs;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks;

internal unsafe class Class_FromName_Hook : Hook<Class_FromName_Hook.MethodDelegate>
{
    private static readonly Dictionary<(IntPtr ImagePointer, string Namespace, string Class), IntPtr> s_ClassNameLookup = new();

    public override string TargetMethodName => "Class::FromName";
    public override MethodDelegate GetDetour() => Hook;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate Il2CppClass* MethodDelegate(Il2CppImage* image, IntPtr _namespace, IntPtr name);

    private Il2CppClass* Hook(Il2CppImage* image, IntPtr _namespace, IntPtr name)
    {
        Il2CppClass* classPtr = Original(image, _namespace, name);

        if (classPtr == null)
        {
            var namespaze = Marshal.PtrToStringUTF8(_namespace) ?? "";
            var className = Marshal.PtrToStringUTF8(name) ?? "";
            s_ClassNameLookup.TryGetValue(((IntPtr)image, namespaze, className), out IntPtr injectedClass);
            classPtr = (Il2CppClass*)injectedClass;
        }

        return classPtr;
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public override IntPtr FindTargetMethod()
    {
        var classFromNameAPI = Il2CppModule.GetExport(nameof(IL2CPP.il2cpp_class_from_name));
        Logger.Instance.LogTrace("il2cpp_class_from_name: 0x{ClassFromNameApiAddress}", classFromNameAPI.ToInt64().ToString("X2"));

        return XrefScanner.JumpTargets(classFromNameAPI).Single();
    }

    internal static void AddTypeToLookup(string imageName, string namespaze, string klass, IntPtr typePointer)
    {
        var image = AssemblyInjector.GetOrCreateImage(imageName).ImagePointer;
        s_ClassNameLookup.Add(((IntPtr)image, namespaze, klass), typePointer);
    }
}
