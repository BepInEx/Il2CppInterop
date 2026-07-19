# Unstripping

Certain assemblies can be unstripped because we have source code available, eg mscorlib and UnityEngine.

## Injection

Unstripped types and members get injected, similar to custom user classes.

## Replace Native Implementation

If the success rate for unstripping is high, and if a certain method can be unstripped, its implementation should be moved to managed land. This enables several things:

* Transpilers (assuming that native is patched to use this implementation)
* Generic instances not present in the game

However, unstripping semantics would have to be perfect. Little flaws are unacceptable.

```cs
static IntPtr GeneratedDynamicMethod(IntPtr @this, FixedSizeStruct_8b param0)
{
    IntPtr result;
    try
    {
        result = Il2CppTypeHelper.ReadFromPointer<Class>(&@this).ManagedMethod(Il2CppTypeHelper.ReadFromPointer<Struct>(&param0)).Box();
    }
    catch (Il2CppException e)
    {
        result = default;
        il2cpp_raise_exception(e.Il2CppObject.Pointer)
    }
    return result;
}
```

## ICalls

Unity internal calls can be recovered.

```cs
// Ignore the use of string, rather than Il2CppSystem.String
public static class SceneUtility
{
    private delegate IntPtr GetScenePathByBuildIndexDelegate(int buildIndex);

    private delegate int GetBuildIndexByScenePathDelegate(IntPtr scenePath);

    private static readonly GetScenePathByBuildIndexDelegate GetScenePathByBuildIndexDelegateField = IL2CPP.ResolveICall<GetScenePathByBuildIndexDelegate>("UnityEngine.SceneManagement.SceneUtility::GetScenePathByBuildIndex");

    private static readonly GetBuildIndexByScenePathDelegate GetBuildIndexByScenePathDelegateField = IL2CPP.ResolveICall<GetBuildIndexByScenePathDelegate>("UnityEngine.SceneManagement.SceneUtility::GetBuildIndexByScenePath");

    public static string GetScenePathByBuildIndex(int buildIndex)
    {
        return IL2CPP.Il2CppStringToManaged(GetScenePathByBuildIndexDelegateField(buildIndex));
    }

    public static int GetBuildIndexByScenePath(string scenePath)
    {
        return GetBuildIndexByScenePathDelegateField(IL2CPP.ManagedStringToIl2Cpp(scenePath));
    }
}
```

## callvirt

```cs
public static T* ThrowIfNull<T>(this T* pointer) where T : struct
{
    if (pointer is null)
    {
        throw new Il2CppSystem.NullReferenceException();
    }
    return pointer;
}
public static T ThrowIfNull<T>(this T obj) where T : class
{
    if (obj is null)
    {
        throw new Il2CppSystem.NullReferenceException();
    }
    return obj;
}
```

Inserting this before any `callvirt` instruction ensures that correct semantics are maintained.

## ldelem/stelem/ldlen

Null checking and bounds checking are handled in the array helper methods.

## Numeric op codes

These are untyped, so some rudimentary analysis is needed to determine the type.
