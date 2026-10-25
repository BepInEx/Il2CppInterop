# Implementing interfaces with injected types

Injected classes and structs can implement Il2Cpp interfaces. Interfaces are generated as actual interfaces, so an
injected type implements them directly:

```cs
[InjectedType]
public partial class ClickHandler : MonoBehaviour, IPointerClickHandler
{
    [Il2CppMethod]
    public void OnPointerClick(PointerEventData eventData)
    {
    }
}
```

Each Il2Cpp interface with methods gets a block in the vtable of the injected class, so Il2Cpp can call the
implementation through the interface. In the example above, both `GetComponent<IPointerClickHandler>()` and the calls
that uGUI makes through `ExecuteEvents` reach `OnPointerClick`.

Interface methods are matched to their implementations with the .NET interface map of the injected type.

Known caveats:

* Every method that implements an Il2Cpp interface method must have `[Il2CppMethod]`, because Il2Cpp calls it.
  Otherwise, registration throws an `InvalidOperationException` that names the method.
* Generic methods cannot be injected, so interfaces with generic methods cannot be implemented.
