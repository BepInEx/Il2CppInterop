namespace Il2CppSystem.Reflection;

public abstract class MonoField : RtFieldInfo
{
    public IObject GetValueInternal(IObject obj) => null;
}
