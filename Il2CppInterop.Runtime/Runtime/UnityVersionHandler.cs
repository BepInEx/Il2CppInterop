using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Extensions;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.AssemblyName;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.EventInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Exception;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.FieldInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.PropertyInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Type;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Runtime;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal class ApplicableToUnityVersionsSinceAttribute : Attribute
{
    public ApplicableToUnityVersionsSinceAttribute(string startVersion)
    {
        StartVersion = startVersion;
    }

    public string StartVersion { get; }
}

public static class UnityVersionHandler
{
    private static readonly Type[] InterfacesOfInterest;
    private static readonly Dictionary<Type, List<(Version Version, object Handler)>> VersionedHandlers = new();
    private static readonly Dictionary<Type, object> Handlers = new();

    internal static INativeAssemblyStructHandler assemblyStructHandler;
    internal static INativeAssemblyNameStructHandler assemblyNameStructHandler;
    internal static INativeClassStructHandler classStructHandler;
    internal static INativeEventInfoStructHandler eventInfoStructHandler;
    internal static INativeExceptionStructHandler exceptionStructHandler;
    internal static INativeFieldInfoStructHandler fieldInfoStructHandler;
    internal static INativeImageStructHandler imageStructHandler;
    internal static INativeMethodInfoStructHandler methodInfoStructHandler;
    internal static INativeParameterInfoStructHandler parameterInfoStructHandler;
    internal static INativePropertyInfoStructHandler propertyInfoStructHandler;
    internal static INativeTypeStructHandler typeStructHandler;

    static UnityVersionHandler()
    {
        var allTypes = GetAllTypesSafe();
        var interfacesOfInterest = allTypes.Where(t =>
                t.IsInterface && typeof(INativeStructHandler).IsAssignableFrom(t) && t != typeof(INativeStructHandler))
            .ToArray();
        InterfacesOfInterest = interfacesOfInterest;

        foreach (var i in interfacesOfInterest) VersionedHandlers[i] = new List<(Version Version, object Handler)>();

        foreach (var handlerImpl in allTypes.Where(t =>
                     !t.IsAbstract && interfacesOfInterest.Any(i => i.IsAssignableFrom(t))))
            foreach (var startVersion in handlerImpl.GetCustomAttributes<ApplicableToUnityVersionsSinceAttribute>())
            {
                var instance = Activator.CreateInstance(handlerImpl);
                foreach (var i in handlerImpl.GetInterfaces())
                    if (interfacesOfInterest.Contains(i))
                        VersionedHandlers[i].Add((Version.Parse(startVersion.StartVersion), instance));
            }

        foreach (var handlerList in VersionedHandlers.Values)
            handlerList.Sort((a, b) => -a.Version.CompareTo(b.Version));

        RecalculateHandlers();
    }

    public static bool HasGetMethodFromReflection { get; private set; }
    public static bool HasShimForGetMethod { get; private set; }
    public static bool IsMetadataV29OrHigher { get; private set; }

    // Version since which extra_arg is set to invoke_multicast, necessitating constructor calls
    public static bool MustUseDelegateConstructor => IsMetadataV29OrHigher;

    internal static void RecalculateHandlers()
    {
        Handlers.Clear();
        var unityVersion = Il2CppInteropRuntime.Instance.UnityVersion;

        foreach (var type in InterfacesOfInterest)
            foreach (var valueTuple in VersionedHandlers[type])
            {
                if (valueTuple.Version > unityVersion) continue;

                Handlers[type] = valueTuple.Handler;
                break;
            }

        HasGetMethodFromReflection = unityVersion > new Version(2018, 1, 0);
        IsMetadataV29OrHigher = unityVersion >= new Version(2021, 2, 0);

        HasShimForGetMethod = unityVersion >= new Version(2020, 3, 41) || IsMetadataV29OrHigher;

        assemblyStructHandler = GetHandler<INativeAssemblyStructHandler>();
        assemblyNameStructHandler = GetHandler<INativeAssemblyNameStructHandler>();
        classStructHandler = GetHandler<INativeClassStructHandler>();
        eventInfoStructHandler = GetHandler<INativeEventInfoStructHandler>();
        exceptionStructHandler = GetHandler<INativeExceptionStructHandler>();
        fieldInfoStructHandler = GetHandler<INativeFieldInfoStructHandler>();
        imageStructHandler = GetHandler<INativeImageStructHandler>();
        methodInfoStructHandler = GetHandler<INativeMethodInfoStructHandler>();
        parameterInfoStructHandler = GetHandler<INativeParameterInfoStructHandler>();
        propertyInfoStructHandler = GetHandler<INativePropertyInfoStructHandler>();
        typeStructHandler = GetHandler<INativeTypeStructHandler>();
    }

    private static T GetHandler<T>()
    {
        if (Handlers.TryGetValue(typeof(T), out var result))
            return (T)result;

        Logger.Instance.LogError("No direct for {TypeFullName} found for Unity {UnityVersion}; this likely indicates a severe error somewhere", typeof(T).FullName, Il2CppInteropRuntime.Instance.UnityVersion);

        throw new ApplicationException("No handler");
    }

    private static Type[] GetAllTypesSafe()
    {
        return typeof(UnityVersionHandler).Assembly.GetTypesSafe();
    }

    //Assemblies
    public static INativeAssemblyStruct NewAssembly()
    {
        return assemblyStructHandler.CreateNewStruct();
    }

    public static unsafe INativeAssemblyStruct Wrap(Il2CppAssembly* assemblyPointer)
    {
        return assemblyStructHandler.Wrap(assemblyPointer);
    }

    public static int AssemblySize()
    {
        return assemblyStructHandler.Size();
    }

    //Assembly Names
    public static INativeAssemblyNameStruct NewAssemblyName()
    {
        return assemblyNameStructHandler.CreateNewStruct();
    }

    public static unsafe INativeAssemblyNameStruct Wrap(Il2CppAssemblyName* assemblyNamePointer)
    {
        return assemblyNameStructHandler.Wrap(assemblyNamePointer);
    }

    public static int AssemblyNameSize()
    {
        return assemblyNameStructHandler.Size();
    }

    //Classes
    public static INativeClassStruct NewClass(int vTableSlots)
    {
        return classStructHandler.CreateNewStruct(vTableSlots);
    }

    public static unsafe INativeClassStruct Wrap(Il2CppClass* classPointer)
    {
        return classStructHandler.Wrap(classPointer);
    }

    public static int ClassSize()
    {
        return classStructHandler.Size();
    }

    //Events
    public static INativeEventInfoStruct NewEvent()
    {
        return eventInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativeEventInfoStruct Wrap(Il2CppEventInfo* eventInfoPointer)
    {
        return eventInfoStructHandler.Wrap(eventInfoPointer);
    }

    public static int EventSize()
    {
        return eventInfoStructHandler.Size();
    }

    //Exceptions
    public static INativeExceptionStruct NewException()
    {
        return exceptionStructHandler.CreateNewStruct();
    }

    public static unsafe INativeExceptionStruct Wrap(Il2CppException* exceptionPointer)
    {
        return exceptionStructHandler.Wrap(exceptionPointer);
    }

    public static int ExceptionSize()
    {
        return exceptionStructHandler.Size();
    }

    //Fields
    public static INativeFieldInfoStruct NewField()
    {
        return fieldInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativeFieldInfoStruct Wrap(Il2CppFieldInfo* fieldInfoPointer)
    {
        return fieldInfoStructHandler.Wrap(fieldInfoPointer);
    }

    public static int FieldInfoSize()
    {
        return fieldInfoStructHandler.Size();
    }


    //Images
    public static INativeImageStruct NewImage()
    {
        return imageStructHandler.CreateNewStruct();
    }

    public static unsafe INativeImageStruct Wrap(Il2CppImage* imagePointer)
    {
        return imageStructHandler.Wrap(imagePointer);
    }

    public static int ImageSize()
    {
        return imageStructHandler.Size();
    }

    //Methods
    public static INativeMethodInfoStruct NewMethod()
    {
        return methodInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativeMethodInfoStruct Wrap(Il2CppMethodInfo* methodPointer)
    {
        return methodInfoStructHandler.Wrap(methodPointer);
    }

    public static int MethodSize()
    {
        return methodInfoStructHandler.Size();
    }

    //Parameters
    public static unsafe Il2CppParameterInfo*[] NewMethodParameterArray(int count)
    {
        return parameterInfoStructHandler.CreateNewParameterInfoArray(count);
    }

    public static unsafe INativeParameterInfoStruct Wrap(Il2CppParameterInfo* parameterInfo)
    {
        return parameterInfoStructHandler.Wrap(parameterInfo);
    }

    public static unsafe INativeParameterInfoStruct Wrap(Il2CppParameterInfo* parameterInfo, int index)
    {
        return parameterInfoStructHandler.Wrap(parameterInfo, index);
    }

    public static bool ParameterInfoHasNamePosToken()
    {
        return parameterInfoStructHandler.HasNamePosToken;
    }


    //Properties
    public static INativePropertyInfoStruct NewProperty()
    {
        return propertyInfoStructHandler.CreateNewStruct();
    }

    public static unsafe INativePropertyInfoStruct Wrap(Il2CppPropertyInfo* propertyInfoPointer)
    {
        return propertyInfoStructHandler.Wrap(propertyInfoPointer);
    }

    public static int ParameterInfoSize()
    {
        return parameterInfoStructHandler.Size();
    }

    //Types
    public static INativeTypeStruct NewType()
    {
        return typeStructHandler.CreateNewStruct();
    }

    public static unsafe INativeTypeStruct Wrap(Il2CppTypeStruct* typePointer)
    {
        return typeStructHandler.Wrap(typePointer);
    }

    public static int TypeSize()
    {
        return typeStructHandler.Size();
    }
}
