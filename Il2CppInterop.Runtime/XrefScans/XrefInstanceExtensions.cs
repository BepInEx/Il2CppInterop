using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using IntPtr = System.IntPtr;
using InvalidOperationException = System.InvalidOperationException;

namespace Il2CppInterop.Runtime.XrefScans;

public static class XrefInstanceExtensions
{
    public static Object? ReadAsObject(this XrefInstance self)
    {
        if (self.Type != XrefType.Global) throw new InvalidOperationException("Can't read non-global xref as object");

        var valueAtPointer = Marshal.ReadIntPtr(self.Pointer);
        if (valueAtPointer == IntPtr.Zero)
            return null;

        return Il2CppObjectPool.Get(valueAtPointer) as Object;
    }

    public static MethodBase? TryResolve(this XrefInstance self)
    {
        if (self.Type != XrefType.Method) throw new InvalidOperationException("Can't resolve non-method xrefs");

        return XrefScanMethodDb.TryResolvePointer(self.Pointer);
    }
}
