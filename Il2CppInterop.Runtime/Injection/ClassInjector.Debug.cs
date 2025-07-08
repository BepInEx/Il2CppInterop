using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection;

public unsafe partial class ClassInjector
{
    public static void Dump<T>()
    {
        Dump((Il2CppClass*)Il2CppClassPointerStore<T>.NativeClassPtr);
    }

    private static string ToString(Il2CppClass* il2CppClass)
    {
        if (il2CppClass == default) return "null";
        var classStruct = UnityVersionHandler.Wrap(il2CppClass);
        return $"{Marshal.PtrToStringUTF8(classStruct.Namespace)}.{Marshal.PtrToStringUTF8(classStruct.Name)}";
    }

    private static string ToString(Il2CppTypeStruct* il2CppType)
    {
        if (il2CppType == default) return "null";
        return IL2CPP.il2cpp_type_get_name((IntPtr)il2CppType);
    }

    public static void Dump(Il2CppClass* il2CppClass)
    {
        if (il2CppClass == default) throw new ArgumentNullException(nameof(il2CppClass));

        InjectorHelpers.Setup();
        InjectorHelpers.ClassInit(il2CppClass);

        var classStruct = UnityVersionHandler.Wrap(il2CppClass);

        Logger.Instance.LogDebug("Dumping {Pointer:X}", classStruct.Pointer);

        Logger.Instance.LogDebug(" Namespace = {Namespace}", Marshal.PtrToStringUTF8(classStruct.Namespace));
        Logger.Instance.LogDebug(" Name = {Name}", Marshal.PtrToStringUTF8(classStruct.Name));

        Logger.Instance.LogDebug(" Parent = {Parent}", ToString(classStruct.Parent));
        Logger.Instance.LogDebug(" InstanceSize = {InstanceSize}", classStruct.InstanceSize);
        Logger.Instance.LogDebug(" NativeSize = {NativeSize}", classStruct.NativeSize);
        Logger.Instance.LogDebug(" ActualSize = {ActualSize}", classStruct.ActualSize);
        Logger.Instance.LogDebug(" Flags = {Flags}", classStruct.Flags);
        Logger.Instance.LogDebug(" ValueType = {ValueType}", classStruct.ValueType);
        Logger.Instance.LogDebug(" EnumType = {EnumType}", classStruct.EnumType);
        Logger.Instance.LogDebug(" IsGeneric = {IsGeneric}", classStruct.IsGeneric);
        Logger.Instance.LogDebug(" Initialized = {Initialized}", classStruct.Initialized);
        Logger.Instance.LogDebug(" InitializedAndNoError = {InitializedAndNoError}", classStruct.InitializedAndNoError);
        Logger.Instance.LogDebug(" SizeInited = {SizeInited}", classStruct.SizeInited);
        Logger.Instance.LogDebug(" HasFinalize = {HasFinalize}", classStruct.HasFinalize);
        Logger.Instance.LogDebug(" IsVtableInitialized = {IsVtableInitialized}", classStruct.IsVtableInitialized);

        var vtable = (VirtualInvokeData*)classStruct.VTable;
        Logger.Instance.LogDebug(" VTable ({VtableCount}):", classStruct.VtableCount);
        for (var i = 0; i < classStruct.VtableCount; i++)
        {
            var virtualInvokeData = vtable![i];
            var methodName = virtualInvokeData.method == default ? "<null>" : Marshal.PtrToStringUTF8(UnityVersionHandler.Wrap(virtualInvokeData.method).Name);

            Logger.Instance.LogDebug("  [{I}] {MethodName} - {MethodPtr}", i, methodName, (virtualInvokeData.methodPtr == default ? "<null>" : virtualInvokeData.methodPtr));
        }

        Logger.Instance.LogDebug(" Fields ({FieldCount}):", classStruct.FieldCount);
        for (var i = 0; i < classStruct.FieldCount; i++)
        {
            var field = UnityVersionHandler.Wrap(classStruct.Fields + i * UnityVersionHandler.FieldInfoSize());

            Logger.Instance.LogDebug($"  [{i}] {ToString(field.Type)} {Marshal.PtrToStringUTF8(field.Name)} - {field.Offset}");
        }

        Logger.Instance.LogDebug(" Methods ({MethodCount}):", classStruct.MethodCount);
        for (var i = 0; i < classStruct.MethodCount; i++)
        {
            var method = UnityVersionHandler.Wrap(classStruct.Methods[i]);

            Logger.Instance.LogDebug("  [{I}] {ReturnType} {Name}({ParametersCount}), {Flags}, {Slot}", i, ToString(method.ReturnType), Marshal.PtrToStringUTF8(method.Name), method.ParametersCount, method.Flags, method.Slot);
        }

        Logger.Instance.LogDebug(" ImplementedInterfaces ({InterfaceCount}):", classStruct.InterfaceCount);
        for (var i = 0; i < classStruct.InterfaceCount; i++)
        {
            var @interface = UnityVersionHandler.Wrap(classStruct.ImplementedInterfaces[i]);

            Logger.Instance.LogDebug("  [{I}] {Name}", i, Marshal.PtrToStringUTF8(@interface.Name));
        }

        Logger.Instance.LogDebug(" InterfaceOffsets ({InterfaceOffsetsCount}):", classStruct.InterfaceOffsetsCount);
        for (var i = 0; i < classStruct.InterfaceOffsetsCount; i++)
        {
            var pair = classStruct.InterfaceOffsets[i];
            var @interface = UnityVersionHandler.Wrap(pair.interfaceType);

            Logger.Instance.LogDebug("  [{I}] {Offset} - {Name}", i, pair.offset, Marshal.PtrToStringUTF8(@interface.Name));
        }

        Logger.Instance.LogDebug(" TypeHierarchy ({TypeHierarchyDepth}):", classStruct.TypeHierarchyDepth);
        for (var i = 0; i < classStruct.TypeHierarchyDepth; i++)
        {
            var @interface = UnityVersionHandler.Wrap(classStruct.TypeHierarchy[i]);

            Logger.Instance.LogDebug("  [{I}] {Name}", i, Marshal.PtrToStringUTF8(@interface.Name));
        }
    }
}
