using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// The runner inserts the production Class::Init region inside this fixture.
// Only module/signature/export lookup and logging are replaced: no native code
// is scanned or executed, and the returned delegates remain rooted here.
internal static unsafe partial class InjectorHelpers
{
    internal static readonly Module Il2CppModule = new();
    internal static readonly Dictionary<string, nint> Exports = new();
    internal static readonly List<string> Lookups = new();
    internal static bool TryGetIl2CppExport(string name, out nint address)
    {
        Lookups.Add(name);
        return Exports.TryGetValue(name, out address);
    }

    private static readonly d_ClassInit SignatureDelegate = SignatureTarget;
    private static readonly d_ClassInit SubstituteDelegate = SubstituteTarget;
    private static void SignatureTarget(Il2CppClass* klass) { }
    private static void SubstituteTarget(Il2CppClass* klass) { }

    internal static int Main()
    {
        bool x86 = RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64;
        nint signature = Marshal.GetFunctionPointerForDelegate(SignatureDelegate);
        nint substitute = Marshal.GetFunctionPointerForDelegate(SubstituteDelegate);
        Console.WriteLine($"Class::Init resolver architecture: {RuntimeInformation.ProcessArchitecture}");

        Check("signature selection is architecture guarded", signature,
            new[] { "mono_class_instance_size" }, x86 ? signature : substitute,
            x86 ? 1 : 0, x86 ? Array.Empty<string>() : new[] { "mono_class_instance_size" });
        Check("second export fallback is preserved", 0,
            new[] { "mono_class_setup_vtable" }, substitute, x86 ? 2 : 0,
            new[] { "mono_class_instance_size", "mono_class_setup_vtable" });
        Check("third export fallback is preserved", 0,
            new[] { "il2cpp_class_has_references" }, substitute, x86 ? 2 : 0,
            new[] { "mono_class_instance_size", "mono_class_setup_vtable", "il2cpp_class_has_references" });
        Check("missing substitutes fail explicitly", 0, Array.Empty<string>(), 0, x86 ? 2 : 0,
            new[] { "mono_class_instance_size", "mono_class_setup_vtable", "il2cpp_class_has_references" });
        return 0;

        void Check(string name, nint scanResult, string[] exports, nint expected,
            int expectedScans, string[] expectedLookups)
        {
            MemoryUtils.Calls = 0;
            MemoryUtils.Result = scanResult;
            Exports.Clear();
            Lookups.Clear();
            foreach (string export in exports) Exports.Add(export, substitute);
            try
            {
                var resolved = FindClassInit();
                if (expected == 0 || Marshal.GetFunctionPointerForDelegate(resolved) != expected)
                    throw new Exception($"{name}: wrong target");
            }
            catch (NotSupportedException) when (expected == 0) { }
            if (MemoryUtils.Calls != expectedScans || !Lookups.SequenceEqual(expectedLookups))
                throw new Exception($"{name}: unexpected signature/export lookup sequence");
            Console.WriteLine("PASS: " + name);
        }
    }
}

internal struct Il2CppClass { }
internal sealed class Module { internal nint BaseAddress => 0; }
internal static class IL2CPP { internal static void il2cpp_class_has_references() { } }
internal static class MemoryUtils
{
    internal sealed class SignatureDefinition
    {
        internal string pattern;
        internal string mask;
        internal bool xref;
    }
    internal static int Calls;
    internal static nint Result;
    internal static nint FindSignatureInModule(Module module, SignatureDefinition signature)
    {
        if (RuntimeInformation.ProcessArchitecture is not (Architecture.X86 or Architecture.X64))
            throw new Exception("Attempted x86 signature scanning on unsupported architecture");
        Calls++;
        return Result;
    }
}
internal sealed class Logger
{
    internal static readonly Logger Instance = new();
    internal void LogTrace(string message, params object[] values) { }
    internal void LogWarning(string message) { }
}
