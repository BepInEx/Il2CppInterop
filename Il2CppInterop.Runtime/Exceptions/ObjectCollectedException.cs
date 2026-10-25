using System;

namespace Il2CppInterop.Runtime.Exceptions;

public class ObjectCollectedException : Exception
{
    public ObjectCollectedException(string message) : base(message)
    {
    }
}
