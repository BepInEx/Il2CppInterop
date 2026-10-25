# Il2Cppmscorlib

This is a stub assembly to enable a circular dependency with Il2CppInterop.Runtime.

To enforce project standards, nearly all classes in this assembly are marked as abstract, even if they aren't actually abstract in the real assembly. Similarly, constructors are marked as protected to avoid misuse. Objects should be created using the `Il2CppObjectPool.Get` API instead.
