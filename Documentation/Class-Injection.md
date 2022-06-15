# Class injection

Managed classes can be injected into Il2Cpp domain. Currently this is fairly limited,
but functional enough for GC integration and implementing custom MonoBehaviors.

## How-to

### Simple case (no need to create new instances from managed code)

The simple case is useful for injecting classes that will be instantiated by IL2CPP (for example MonoBehaviours).

* Create a class that inherits an IL2CPP class.
* Add methods and fields normally.
* Call `ClassInjector.RegisterTypeInIl2Cpp<T>()` before first use of class to be injected

Example:

```c#
public class MyMonoBehaviour : MonoBehaviour
{
    void Awake()
    {
        // Normal Awake
    }
}

// Example use:

var go = new GameObject();
// This is OK; class is instantiated by IL2CPP
go.AddComponent<MyMonoBehaviour>();
```

Notes:

* **Do not instantiate the class manually**, e.g. `new MyMonoBehaviour()`. Instead use IL2CPP methods that will
  instantiate the class for you.
* If you have a pointer that you want to convert to `MyMonoBehaviour*`,
  use `new Il2CppObjectBase(pointer).Cast<MyMonoBehaiour>();`.

### Extended case (need to create new instances from managed code)

* Your class must inherit from an IL2CPP class.
* You must include a constructor that takes `IntPtr` and passes it to base class constructor. It will be called when
  objects of your class are created from IL2CPP side.
* To create your object from managed side, call base class `IntPtr` constructor with result
  of `ClassInjector.DerivedConstructorPointer<T>()`, where T is your class type, and
  call `ClassInjector.DerivedConstructorBody(this)` in constructor body.
* Call `ClassInjector.RegisterTypeInIl2Cpp<T>()` before first use of class to be injected

Example:

```c#
public class MyClass : SomeIL2CPPClass
{
    // Used by IL2CPP when creating new instances of this class
    public MyClass(IntPtr ptr) : base(ptr) { }
    
    // Used by managed code when creating new instances of this class
    public MyClass() : base(ClassInjector.DerivedConstructorPointer<MyClass>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }
    
    // Any other methods
}


// Example use:

// Creates a new instance of MyClass in IL2CPP
var myInstance = new MyClass();

// If you have a pointer that you want to convert to MyClass, you can use the IntPtr constructor for convenience
var someInstance = new MyClass(pointer);
```

## Fine-tuning

* `[HideFromIl2Cpp]` can be used to prevent a method from being exposed to il2cpp

## Caveats

* Injected class instances are handled by IL2CPP garbage collection. This means that an object may be collected even if
  it's referenced from managed domain. Attempting to use that object afterwards will result
  in `ObjectCollectedException`. Conversely, managed representation of injected object will not be garbage collected as
  long as it's referenced from IL2CPP domain.
* It might be possible to create a cross-domain reference loop that will prevent objects from being garbage collected.
  Avoid doing anything that will result in injected class instances (indirectly) storing references to itself. The
  simplest example of how to leak memory is this:

```c#
class Injected: Il2CppSystem.Object {
    Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object> list = new ...;
    public Injected() {
        list.Add(this); // reference to itself through an IL2CPP list. This will prevent both this and list from being garbage collected, ever.
    }
}
```

## Fields injection

> TODO: Describe how field injection works based on [#24](https://github.com/BepInEx/Il2CppAssemblyUnhollower/pull/24)

## Current limitations

* Not all members are exposed to Il2Cpp side - no properties, events or static methods will be visible to
  Il2Cpp reflection. Fields are exported, but the feature is fairly limited.
* Only a limited set of types is supported for method signatures
 