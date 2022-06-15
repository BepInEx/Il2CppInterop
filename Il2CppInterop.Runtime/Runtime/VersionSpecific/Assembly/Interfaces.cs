using Il2CppInterop.Runtime.Runtime.VersionSpecific.AssemblyName;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;

public interface INativeAssemblyStructHandler : INativeStructHandler
{
    INativeAssemblyStruct CreateNewStruct();
    unsafe INativeAssemblyStruct Wrap(Il2CppAssembly* assemblyPointer);
}

public interface INativeAssemblyStruct : INativeStruct
{
    unsafe Il2CppAssembly* AssemblyPointer { get; }

    unsafe ref Il2CppImage* Image { get; }

    INativeAssemblyNameStruct Name { get; }
}
