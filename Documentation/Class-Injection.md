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

## Garbage Collection

* Managed instances of injected classes hold strong handles to their unmanaged counterparts.
  This means it is unlikely for an unmanaged object to be collected while it is referenced from the managed domain.
  If this rare case does occur, an attempt to use to injected instance will throw an `ObjectCollectedException`.
* If there are no extant references to the managed instance, it will be garbage collected,
  releasing the strong handle it holds and allowing the underlying unmanaged object to eventually be collected in turn.
  When this occurs, however, the finalizer for the managed instance will not be run,
  delaying execution until the unmanaged object is also ready to be garbage collected.
* The unmanaged-to-managed mapping is a one-to-many relationship.
  While during typical execution there will be exactly zero, or exactly one extant managed object,
  in certain situations, such as when the object pool is disabled, there may be any number.#
* Finalizers are supported for injected classes, but note that the managed injector (`~MyClass`) is not invoked predictably.
  If you want to implement a custom finalizer, define a `private void Il2CppFinalize()` method on your class (see below).
* Due to the implementation of finalizers, the "resurrection pattern" does not translate directly to injected types.
  If you do not call `ClassInjector.ResurrectObject` during an object's finalizer,
  the unmanaged object will be garbage collected and future use will throw `ObjectCollectedException`s.
  **NB:** You will still also need to call `Il2CppSystem.GC.ReRegisterForFinalize` if applicable.

<details>
<summary>A detailed example of finalizer behavior for injected classes</summary>
<br>

Consider the following example:

```c#
class Foo : Il2CppSystem.Object
{
    public Foo(IntPtr ptr) : base(ptr) { }
    public Foo() : this(ClassInjector.DerivedConstructorPointer<Foo>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    private void Il2CppFinalize()
    {
        // ...
    }
}

ClassInjector.RegisterTypeInIl2Cpp<Foo>();

Foo foobar = new();
// ... function ends ...
```

In the above example, the events occur in this order:

1. `Foo` is injected into the il2cpp domain:
  * The class injector walks the superclasses of `Foo` and collects all the `Il2CppFinalize` methods.
  * These finalizers are organised in a chain from subclass to superclass.
  * The injected class is created with an unmanaged finalizer which we can hook into later.
2. The parameterless `Foo` constructor is called:
  * An unmanaged instance of the injected class is created.
  * A strong handle to the unmanaged instance is put into the managed instance of `Foo`.
3. The managed instance of `Foo` goes out of scope, allowing this instance to be garbage collected.
4. Some time later, the managed GC will run its finalizer:
  * Eventually, the GC reaches the finalizer of `Il2CppObjectBase`
    where the strong handle to the unmanaged instance is released.
  * Assuming no other managed or unmanaged references to the unmanaged object exist,
    the unmanaged instance of `Foo` can now be garbage collected.
  * Here, the managed instance of `Foo` is destroyed by the garbage collector,
    but the unmanaged instance still exists temporarily.
5. When the unmanaged GC acknowleges this, the unmanaged object's finalizer is called, firing the hook we installed:
  * A fresh managed instance of `Foo` is created.
  * This fresh instance is "downgraded" and its strong handle becomes a weak handle,
    preventing this instance from blocking the garbage collection of the unmanaged instance.
  * We follow the links in the finalizer chain, running the user-specified finalization method (finally!).
6. The unmanaged object, which has no extant references, is garbage collected with its managed finalizer having run exactly once.

Note that this complexity is present only for injected classes with finalizers.
Injected classes without an `Il2CppFinalize` method are collected without running any hooks.

</details>

## Fields injection

> TODO: Describe how field injection works based on [#24](https://github.com/BepInEx/Il2CppAssemblyUnhollower/pull/24)

## Current limitations

* Not all members are exposed to Il2Cpp side - no properties, events or static methods will be visible to
  Il2Cpp reflection. Fields are exported, but the feature is fairly limited.
* Only a limited set of types is supported for method signatures
