# Enums

Despite the complexity involved, enums should be reproduced as-is.

```cs
public enum ElectricityType
{
    Off = 0,
    On = 1
}
```

## Il2Cpp Initialization

Il2Cpp initialization may be complicated.

### Option 1: `[OriginalName]` Attribute

This was the Il2CppInterop approach.

```cs
[OriginalName("Assembly-CSharp.dll", "", "ElectricityType")]
public enum ElectricityType
{
    Off = 0,
    On = 1
}
```

### Option 2: `[InitializerType]` Attribute

This allows storing field pointers.

```cs
[InitializerType(typeof(ElectricityType_UniqueHash))]
public enum ElectricityType
{
    Off = 0,
    On = 1
}
internal static class ElectricityType_UniqueHash
{
    static ElectricityType_UniqueHash()
    {
        Il2CppClassPointerStore<ElectricityType>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "ElectricityType");
        IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<ElectricityType>.NativeClassPtr);
        // Field pointers are also stored in this class.
    }
}
```

### Option 3: Module Initialization

```cs
public enum ElectricityType
{
    Off = 0,
    On = 1
}
internal static class ElectricityType_UniqueHash
{
    internal static void Initialize()
    {
        Il2CppClassPointerStore<ElectricityType>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "ElectricityType");
        IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<ElectricityType>.NativeClassPtr);
        // Field pointers are also stored in this class.
    }
}
internal static class ModuleInitialization
{
    internal static void Initialize()
    {
        ElectricityType_UniqueHash.Initialize();
        // And all the other types too.
    }
}
```

### Option 4: Global Initialization

```cs
public enum ElectricityType
{
    Off = 0,
    On = 1
}
internal static class ElectricityType_UniqueHash
{
    internal static void Initialize()
    {
        Il2CppClassPointerStore<ElectricityType>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "ElectricityType");
        IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<ElectricityType>.NativeClassPtr);
        // Field pointers are also stored in this class.
    }
}
public static class ModuleInitialization_AssemblyNameHash
{
    public static void Initialize()
    {
        ElectricityType_UniqueHash.Initialize();
        // And all the other types too.
    }
}

// Il2CppInitialization.dll
public static class Il2CppInitialization
{
    public static void Initialize()
    {
        ModuleInitialization_AssemblyNameHash.Initialize();
        // And all the other assemblies too.
    }
}
```

## Generic constraint

`Il2CppSystem.Enum` should be replaced with `System.Enum` in generic constraints.
