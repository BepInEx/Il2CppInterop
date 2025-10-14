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

    public nint Pointer => default;

    public bool WasCollected => default;
    public void CreateGCHandle(nint objHdl)
    {
    }
}
