using System;

namespace Il2CppInterop.Runtime.Runtime
{
    public interface INativeStructHandler
    {
        public int Size();
    }

    public interface INativeStruct
    {
        IntPtr Pointer { get; }
    }
}
