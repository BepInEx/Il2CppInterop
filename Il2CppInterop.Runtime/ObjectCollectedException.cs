using System;

namespace Il2CppInterop.Runtime
{
    public class ObjectCollectedException : Exception
    {
        public ObjectCollectedException(string message) : base(message)
        {
        }
    }
}