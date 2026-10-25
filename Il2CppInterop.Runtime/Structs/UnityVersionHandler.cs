using System;
using System.Runtime.InteropServices;
using AssetRipper.Primitives;
using Il2CppInterop.Runtime.Startup;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Structs.VersionSpecific.AssemblyName;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Class;
using Il2CppInterop.Runtime.Structs.VersionSpecific.EventInfo;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Exception;
using Il2CppInterop.Runtime.Structs.VersionSpecific.FieldInfo;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Image;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.Structs.VersionSpecific.ParameterInfo;
using Il2CppInterop.Runtime.Structs.VersionSpecific.PropertyInfo;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Type;

namespace Il2CppInterop.Runtime.Structs;

public static partial class UnityVersionHandler
{
    static UnityVersionHandler()
    {
        RecalculateHandlers();
    }

    public static bool HasGetMethodFromReflection { get; private set; }
    public static bool HasShimForGetMethod { get; private set; }
    public static bool IsMetadataV29OrHigher { get; private set; }

    // Version since which extra_arg is set to invoke_multicast, necessitating constructor calls
    public static bool MustUseDelegateConstructor => IsMetadataV29OrHigher;

    internal static void RecalculateHandlers()
    {
        var unityVersion = Il2CppInteropRuntime.Instance.UnityVersion;

        HasGetMethodFromReflection = unityVersion.GreaterThanOrEquals(2018, 2, 0, UnityVersionType.Beta, 6);
        IsMetadataV29OrHigher = unityVersion.GreaterThanOrEquals(2021, 2, 0);

        HasShimForGetMethod = unityVersion.GreaterThanOrEquals(2020, 3, 41) || IsMetadataV29OrHigher;

        SetAssemblyNameStructHandler(unityVersion);
        SetAssemblyStructHandler(unityVersion);
        SetClassStructHandler(unityVersion);
        SetEventInfoStructHandler(unityVersion);
        SetExceptionStructHandler(unityVersion);
        SetFieldInfoStructHandler(unityVersion);
        SetImageStructHandler(unityVersion);
        SetMethodInfoStructHandler(unityVersion);
        SetParameterInfoStructHandler(unityVersion);
        SetPropertyInfoStructHandler(unityVersion);
        SetTypeStructHandler(unityVersion);
    }

    //Assemblies
    public static INativeAssemblyStruct NewAssembly()
    {
        return AssemblyStructHandler.CreateNewStruct();
    }

    public static unsafe INativeAssemblyStruct Wrap(Il2CppAssembly* assemblyPointer)
    {
        return AssemblyStructHandler.Wrap(assemblyPointer);
    }

    public static int AssemblySize()
    {
        return AssemblyStructHandler.Size;
    }

    //Assembly Names
    public static INativeAssemblyNameStruct NewAssemblyName()
    {
        return AssemblyNameStructHandler.CreateNewStruct();
    }

    public static unsafe INativeAssemblyNameStruct Wrap(Il2CppAssemblyName* assemblyNamePointer)
    {
        return AssemblyNameStructHandler.Wrap(assemblyNamePointer);
    }

    public static int AssemblyNameSize()
    {
        return AssemblyNameStructHandler.Size;
    }

    //Classes
    public static INativeClassStruct NewClass(int vTableSlots)
    {
        return ClassStructHandler.CreateNewStruct(vTableSlots);
    }

    public static unsafe INativeClassStruct Wrap(Il2CppClass* classPointer)
    {
        return ClassStructHandler.Wrap(classPointer);
    }

    public static int ClassSize()
    {
        return ClassStructHandler.Size;
    }

    //Events
    public static INativeEventInfoStruct NewEvent()
    {
        return EventInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativeEventInfoStruct Wrap(Il2CppEventInfo* eventInfoPointer)
    {
        return EventInfoStructHandler.Wrap(eventInfoPointer);
    }

    public static int EventSize()
    {
        return EventInfoStructHandler.Size;
    }

    //Exceptions
    public static INativeExceptionStruct NewException()
    {
        return ExceptionStructHandler.CreateNewStruct();
    }

    public static unsafe INativeExceptionStruct Wrap(Il2CppException* exceptionPointer)
    {
        return ExceptionStructHandler.Wrap(exceptionPointer);
    }

    public static int ExceptionSize()
    {
        return ExceptionStructHandler.Size;
    }

    //Fields
    public static INativeFieldInfoStruct NewField()
    {
        return FieldInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativeFieldInfoStruct Wrap(Il2CppFieldInfo* fieldInfoPointer)
    {
        return FieldInfoStructHandler.Wrap(fieldInfoPointer);
    }

    public static int FieldInfoSize()
    {
        return FieldInfoStructHandler.Size;
    }

    //Images
    public static INativeImageStruct NewImage()
    {
        return ImageStructHandler.CreateNewStruct();
    }

    public static unsafe INativeImageStruct Wrap(Il2CppImage* imagePointer)
    {
        return ImageStructHandler.Wrap(imagePointer);
    }

    public static int ImageSize()
    {
        return ImageStructHandler.Size;
    }

    //Methods
    public static INativeMethodInfoStruct NewMethod()
    {
        return MethodInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativeMethodInfoStruct Wrap(Il2CppMethodInfo* methodPointer)
    {
        return MethodInfoStructHandler.Wrap(methodPointer);
    }

    public static int MethodSize()
    {
        return MethodInfoStructHandler.Size;
    }

    //Parameters
    public static unsafe Il2CppParameterInfo*[] NewMethodParameterArray(int count)
    {
        if (count == 0)
            return [];

        var elementSize = ParameterInfoStructHandler.Size;
        var totalSize = elementSize * count;
        var startPointer = Marshal.AllocHGlobal(totalSize);
        new Span<byte>(startPointer.ToPointer(), totalSize).Clear();
        var result = new Il2CppParameterInfo*[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = (Il2CppParameterInfo*)(startPointer + i * elementSize);
        }
        return result;
    }

    public static unsafe INativeParameterInfoStruct Wrap(Il2CppParameterInfo* parameterInfo)
    {
        return ParameterInfoStructHandler.Wrap(parameterInfo);
    }

    public static unsafe INativeParameterInfoStruct Wrap(Il2CppParameterInfo* parameterInfo, int index)
    {
        var address = (nint)parameterInfo + index * ParameterInfoStructHandler.Size;
        return ParameterInfoStructHandler.Wrap((Il2CppParameterInfo*)address);
    }

    //Properties
    public static INativePropertyInfoStruct NewProperty()
    {
        return PropertyInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativePropertyInfoStruct Wrap(Il2CppPropertyInfo* propertyInfoPointer)
    {
        return PropertyInfoStructHandler.Wrap(propertyInfoPointer);
    }

    public static int ParameterInfoSize()
    {
        return ParameterInfoStructHandler.Size;
    }

    //Types
    public static INativeTypeStruct NewType()
    {
        return TypeStructHandler.CreateNewStruct();
    }

    public static unsafe INativeTypeStruct Wrap(Il2CppTypeStruct* typePointer)
    {
        return TypeStructHandler.Wrap(typePointer);
    }

    public static int TypeSize()
    {
        return TypeStructHandler.Size;
    }
}
