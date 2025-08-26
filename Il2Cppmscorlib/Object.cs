namespace Il2CppSystem;

public class Object
{
    public bool isWrapped;
    public nint pooledPtr;

    public Object()
    {
    }

    public Object(nint ptr)
    {
    }

    public nint ObjectClass => default;

    public nint Pointer => default;

    public bool WasCollected => default;
    public void CreateGCHandle(nint objHdl) { }
    public T Cast<T>() where T : Object
    {
        return default;
    }

    public static unsafe T UnboxUnsafe<T>(nint pointer)
    {
        return default;
    }

    public T Unbox<T>() where T : unmanaged
    {
        return UnboxUnsafe<T>(Pointer);
    }

    public static class InitializerStore<T>
    {
        public static System.Func<nint, T> Initializer => null;
    }
    public T TryCast<T>() where T : Object
    {
        return null;
    }
}
