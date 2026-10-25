# Il2Cpp Initialization

Every Il2Cpp class has a generated `Il2CppInternals` class.

## Module Initialization

The static constructor for a type triggers its Il2Cpp initialization.

## Global Initialization

A global initialization assembly ensures that all types get initialized on startup. This is essential for ensuring that the object pool can create objects.
