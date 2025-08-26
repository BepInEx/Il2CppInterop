# Exceptions

```cs
public abstract class Il2CppException : Exception
{
}
public sealed class Il2CppException<T> : Il2CppException where T : Il2CppSystem.Exception
{
}
```
