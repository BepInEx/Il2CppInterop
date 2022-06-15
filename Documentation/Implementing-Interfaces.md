# Implementing interfaces with injected types

Injected types can implement Il2Cpp interfaces.  
Just like previously, your type can't implement the interface directly, as it's still generated as a class.  
However, you can pass additional interface types to `RegisterTypeInIl2Cpp<T>(RegisterTypeOptions options)`, and they
will be implemented as interfaces on the IL2CPP version of your type.  
Interface methods are matched to methods in your class by name, parameter count and genericness.

Known caveats:

* `obj.Cast<InterfaceType>()` will fail if you try to cast an object of your injected type to an interface. You can work
  around that with `new InterfaceType(obj.Pointer)` if you're absolutely sure it implements that interface.
* Limited method matching might result in some interfaces being trickier or impossible to implement, namely those with
  overloads differing only in parameter types.