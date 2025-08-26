# Arrays

## Array Types

Il2Cpp arrays are represented with a closed type hierarchy.

* `Il2CppArrayBase`
* `Il2CppArrayBase<T>`
* `Il2CppReferenceArray<T>`
* `Il2CppObjectArray`
* `Il2CppInterfaceArray<T>`
* `Il2CppBlittableArray<T>`
* `Il2CppNonBlittableArray<T>`

`Il2CppArrayBase<T>` is the type used everywhere. The others are just for construction.
