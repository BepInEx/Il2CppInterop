using System;

namespace UnhollowerBaseLib.Runtime
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
