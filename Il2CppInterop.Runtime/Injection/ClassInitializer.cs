using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection.Hooks;
using Il2CppInterop.Runtime.Structs;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection;

internal static unsafe class ClassInitializer
{
    private delegate void d_ClassInit(Il2CppClass* klass);
    private static readonly d_ClassInit ClassInit = FindClassInit();

    public static void Invoke(Il2CppClass* klass)
    {
        ClassInit.Invoke(klass);
    }

    private static IEnumerable<SignatureDefinition> s_ClassInitSignatures =>
    [
        new SignatureDefinition
        {
            pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x28\x83",
            mask = "x????xxxxx",
            xref = true
        },
        new SignatureDefinition
        {
            pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x48\x48",
            mask = "x????xxxxx",
            xref = true
        }
    ];

    private static d_ClassInit FindClassInit()
    {
        static nint GetClassInitSubstitute()
        {
            if (Il2CppModule.TryGetExport("mono_class_instance_size", out var classInit))
            {
                Logger.Instance.LogTrace("Picked mono_class_instance_size as a Class::Init substitute");
                return classInit;
            }
            if (Il2CppModule.TryGetExport("mono_class_setup_vtable", out classInit))
            {
                Logger.Instance.LogTrace("Picked mono_class_setup_vtable as a Class::Init substitute");
                return classInit;
            }
            if (Il2CppModule.TryGetExport(nameof(IL2CPP.il2cpp_class_has_references), out classInit))
            {
                Logger.Instance.LogTrace("Picked il2cpp_class_has_references as a Class::Init substitute");
                return classInit;
            }

            Logger.Instance.LogTrace("GameAssembly.dll: 0x{Il2CppModuleAddress}", Il2CppModule.Module.BaseAddress.ToInt64().ToString("X2"));
            throw new NotSupportedException("Failed to use signature for Class::Init and a substitute cannot be found, please create an issue and report your unity version & game");
        }
        var pClassInit = s_ClassInitSignatures
            .Select(s => SignatureDefinition.FindSignatureInModule(Il2CppModule.Module, s))
            .FirstOrDefault(p => p != 0);

        if (pClassInit == 0)
        {
            Logger.Instance.LogWarning("Class::Init signatures have been exhausted, using a substitute!");
            pClassInit = GetClassInitSubstitute();
        }

        Logger.Instance.LogTrace("Class::Init: 0x{PClassInitAddress}", pClassInit.ToString("X2"));

        return Marshal.GetDelegateForFunctionPointer<d_ClassInit>(pClassInit);
    }
}
