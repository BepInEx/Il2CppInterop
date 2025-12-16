using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;

namespace Il2CppInterop.Runtime.Injection;

public unsafe class Il2CppInterfaceCollection : List<INativeClassStruct>
{
    public Il2CppInterfaceCollection(IEnumerable<INativeClassStruct> interfaces) : base(interfaces)
    {
    }

    public Il2CppInterfaceCollection(IEnumerable<Type> interfaces) : base(ResolveNativeInterfaces(interfaces))
    {
    }

    private static IEnumerable<INativeClassStruct> ResolveNativeInterfaces(IEnumerable<Type> interfaces)
    {
        return interfaces.Select(it =>
        {
            var classPointer = Il2CppClassPointerStore.GetNativeClassPointer(it);
            if (classPointer == IntPtr.Zero)
                throw new ArgumentException(
                    $"Type {it} doesn't have an IL2CPP class pointer, which means it's not an IL2CPP interface");
            return UnityVersionHandler.Wrap((Il2CppClass*)classPointer);
        });
    }

    public static implicit operator Il2CppInterfaceCollection(INativeClassStruct[] interfaces)
    {
        return new(interfaces);
    }

    public static implicit operator Il2CppInterfaceCollection(Type[] interfaces)
    {
        return new(interfaces);
    }
}
