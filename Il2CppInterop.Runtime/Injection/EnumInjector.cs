using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppSystem;
using Microsoft.Extensions.Logging;
using ArgumentException = System.ArgumentException;
using Convert = System.Convert;
using Enum = System.Enum;
using IntPtr = System.IntPtr;
using Type = System.Type;

namespace Il2CppInterop.Runtime.Injection;

public static unsafe class EnumInjector
{
    // fieldInfo : defaultValueBlob
    private static readonly ConcurrentDictionary<IntPtr, IntPtr> s_DefaultValueOverrides = new();

    private static readonly IntPtr value__Cached = Marshal.StringToCoTaskMemUTF8("value__");

    internal static bool GetDefaultValueOverride(Il2CppFieldInfo* fieldInfo, out IntPtr defaultValueBlob)
    {
        return s_DefaultValueOverrides.TryGetValue((IntPtr)fieldInfo, out defaultValueBlob);
    }

    public static void InjectEnumValues<TEnum>(Dictionary<string, object> valuesToAdd) where TEnum : Enum
    {
        InjectEnumValues(typeof(TEnum), valuesToAdd);
    }

    public static void InjectEnumValues(Type type, Dictionary<string, object> valuesToAdd)
    {
        if (type == null)
            throw new ArgumentException("Type argument cannot be null");
        if (!type.IsEnum)
            throw new ArgumentException("Type argument needs to be an enum");

        var enumPtr = Il2CppClassPointerStore.GetNativeClassPointer(type);
        if (enumPtr == IntPtr.Zero)
            throw new ArgumentException("Type needs to be an Il2Cpp enum");

        InjectorHelpers.Setup();

        InjectorHelpers.ClassInit((Il2CppClass*)enumPtr);

        var il2cppEnum = UnityVersionHandler.Wrap((Il2CppClass*)enumPtr);
        var newFieldCount = il2cppEnum.FieldCount + valuesToAdd.Count;
        var newFields = (Il2CppFieldInfo*)Marshal.AllocHGlobal(newFieldCount * UnityVersionHandler.FieldInfoSize());

        int fieldIdx;
        for (fieldIdx = 0; fieldIdx < il2cppEnum.FieldCount; ++fieldIdx)
        {
            var offset = fieldIdx * UnityVersionHandler.FieldInfoSize();
            var oldField = UnityVersionHandler.Wrap(il2cppEnum.Fields + offset);
            var newField = UnityVersionHandler.Wrap(newFields + offset);

            newField.Name = oldField.Name;
            newField.Type = oldField.Type;
            newField.Parent = oldField.Parent;
            newField.Offset = oldField.Offset;

            // Move the default value blob from the old field to the new one
            if (s_DefaultValueOverrides.TryRemove((IntPtr)oldField.FieldInfoPointer, out var blob))
                s_DefaultValueOverrides[(IntPtr)newField.FieldInfoPointer] = blob;
        }

        var enumElementType = UnityVersionHandler.Wrap(il2cppEnum.ElementClass).ByValArg;

        foreach (var newData in valuesToAdd)
        {
            var offset = fieldIdx * UnityVersionHandler.FieldInfoSize();
            var newField = UnityVersionHandler.Wrap(newFields + offset);
            newField.Name = Marshal.StringToCoTaskMemUTF8(newData.Key);
            newField.Type = enumElementType.TypePointer;
            newField.Parent = il2cppEnum.ClassPointer;
            newField.Offset = 0;

            CreateOrUpdateFieldDefaultValue(newField.FieldInfoPointer, enumElementType.TypePointer, newData.Value);

            ++fieldIdx;
        }

        il2cppEnum.FieldCount = (ushort)newFieldCount;
        il2cppEnum.Fields = newFields;

        var runtimeEnumType = Il2CppType.TypeFromPointer(enumPtr).TryCast<RuntimeType>();
        if (runtimeEnumType != null)
            // The mono runtime caches the enum names and values the first time they are requested, so we reset this cache
            runtimeEnumType.GenericCache = null;
    }

    private static int GetEnumElementSize(Il2CppTypeEnum type)
    {
        return type switch
        {
            Il2CppTypeEnum.IL2CPP_TYPE_I1 => sizeof(sbyte),
            Il2CppTypeEnum.IL2CPP_TYPE_U1 => sizeof(byte),

            Il2CppTypeEnum.IL2CPP_TYPE_CHAR => sizeof(char),

            Il2CppTypeEnum.IL2CPP_TYPE_I2 => sizeof(short),
            Il2CppTypeEnum.IL2CPP_TYPE_U2 => sizeof(ushort),

            Il2CppTypeEnum.IL2CPP_TYPE_I4 => sizeof(int),
            Il2CppTypeEnum.IL2CPP_TYPE_U4 => sizeof(uint),

            Il2CppTypeEnum.IL2CPP_TYPE_I8 => sizeof(long),
            Il2CppTypeEnum.IL2CPP_TYPE_U8 => sizeof(ulong),

            _ => throw new ArgumentException($"The type provided {type} is invalid")
        };
    }

    private static IntPtr AllocateNewDefaultValueBlob(Il2CppTypeEnum type)
    {
        var size = GetEnumElementSize(type);
        var blob = Marshal.AllocHGlobal(size);
        Logger.Instance.LogTrace("Allocated default value blob at 0x{Blob} of {Size} for {Type}", blob.ToInt64().ToString("X2"), size, type);
        return blob;
    }

    private static IntPtr CreateOrUpdateFieldDefaultValue(Il2CppFieldInfo* field, Il2CppTypeStruct* type, object value)
    {
        var typeEnum = UnityVersionHandler.Wrap(type).Type;

        if (!GetDefaultValueOverride(field, out var newBlob))
        {
            newBlob = AllocateNewDefaultValueBlob(typeEnum);
            s_DefaultValueOverrides[(IntPtr)field] = newBlob;
        }

        SetFieldDefaultValue(newBlob, typeEnum, value);
        return newBlob;
    }

    private static void SetFieldDefaultValue(IntPtr blob, Il2CppTypeEnum type, object value)
    {
        var valueData = Convert.ToInt64(value);
        switch (type)
        {
            case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                *(sbyte*)blob = (sbyte)valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                *(byte*)blob = (byte)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                *(char*)blob = (char)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                *(short*)blob = (short)valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                *(ushort*)blob = (ushort)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                *(int*)blob = (int)valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                *(uint*)blob = (uint)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                *(long*)blob = valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                *(ulong*)blob = (ulong)valueData;
                break;

            default: throw new ArgumentException($"The type provided {type} is invalid");
        }
    }

    public static void RegisterEnumInIl2Cpp<TEnum>(bool logSuccess = true) where TEnum : Enum
    {
        RegisterEnumInIl2Cpp(typeof(TEnum), logSuccess);
    }

    public static void RegisterEnumInIl2Cpp(Type type, bool logSuccess = true)
    {
        if (type == null)
            throw new ArgumentException("Type argument cannot be null");

        if (!type.IsEnum)
            throw new ArgumentException("Type argument needs to be an enum");

        var enumPtr = Il2CppClassPointerStore.GetNativeClassPointer(type);
        if (enumPtr != IntPtr.Zero)
            return;

        InjectorHelpers.Setup();

        var baseEnum =
            UnityVersionHandler.Wrap((Il2CppClass*)Il2CppClassPointerStore<Il2CppSystem.Enum>.NativeClassPtr);

        InjectorHelpers.ClassInit(baseEnum.ClassPointer);

        var il2cppEnum = UnityVersionHandler.NewClass(baseEnum.VtableCount);
        var elementClass =
            UnityVersionHandler.Wrap(
                (Il2CppClass*)Il2CppClassPointerStore.GetNativeClassPointer(Enum.GetUnderlyingType(type)));

        il2cppEnum.Image = InjectorHelpers.InjectedImage.ImagePointer;
        il2cppEnum.Class = il2cppEnum.CastClass = il2cppEnum.ElementClass = elementClass.ClassPointer;
        il2cppEnum.Parent = baseEnum.ClassPointer;
        il2cppEnum.ActualSize = il2cppEnum.InstanceSize =
            (uint)(baseEnum.InstanceSize + GetEnumElementSize(elementClass.ByValArg.Type));
        il2cppEnum.NativeSize = -1;

        il2cppEnum.ValueType = true;
        il2cppEnum.EnumType = true;
        il2cppEnum.Initialized = true;
        il2cppEnum.InitializedAndNoError = true;
        il2cppEnum.SizeInited = true;
        il2cppEnum.HasFinalize = true;
        il2cppEnum.IsVtableInitialized = true;

        il2cppEnum.Name = Marshal.StringToCoTaskMemUTF8(type.Name);
        il2cppEnum.Namespace = Marshal.StringToCoTaskMemUTF8(type.Namespace ?? string.Empty);

        var token = InjectorHelpers.CreateClassToken(il2cppEnum.Pointer);
        il2cppEnum.ThisArg.Data = il2cppEnum.ByValArg.Data = (IntPtr)token;

        // Has to be IL2CPP_TYPE_VALUETYPE because IL2CPP_TYPE_ENUM isn't used
        il2cppEnum.ThisArg.Type = il2cppEnum.ByValArg.Type = Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE;

        il2cppEnum.Flags = (Il2CppClassAttributes)type.Attributes;

        il2cppEnum.VtableCount = baseEnum.VtableCount;
        var vtable = (VirtualInvokeData*)il2cppEnum.VTable;
        var baseVTable = (VirtualInvokeData*)baseEnum.VTable;
        for (var i = 0; i < baseEnum.VtableCount; i++)
            vtable[i] = baseVTable[i];

        var enumValues = Enum.GetValues(type);
        var enumNames = Enum.GetNames(type);
        il2cppEnum.FieldCount = (ushort)(enumValues.Length + 1); // value__

        var il2cppFields =
            (Il2CppFieldInfo*)Marshal.AllocHGlobal(il2cppEnum.FieldCount * UnityVersionHandler.FieldInfoSize());
        var valueField = UnityVersionHandler.Wrap(il2cppFields);
        valueField.Name = value__Cached;
        valueField.Parent = il2cppEnum.ClassPointer;
        valueField.Offset = (int)baseEnum.InstanceSize;

        var enumValueType = UnityVersionHandler.NewType();
        enumValueType.Data = elementClass.ThisArg.Data;
        enumValueType.Attrs = (ushort)(FieldAttributes.Private | FieldAttributes.Family | FieldAttributes.SpecialName |
                                        FieldAttributes.RTSpecialName);
        enumValueType.Type = elementClass.ThisArg.Type;
        enumValueType.ByRef = elementClass.ThisArg.ByRef;
        enumValueType.Pinned = elementClass.ThisArg.Pinned;

        valueField.Type = enumValueType.TypePointer;

        var enumConstType = UnityVersionHandler.NewType();
        enumConstType.Data = il2cppEnum.ThisArg.Data;
        enumConstType.Attrs = (ushort)(FieldAttributes.Private | FieldAttributes.Family | FieldAttributes.InitOnly |
                                        FieldAttributes.Literal | FieldAttributes.HasDefault);
        enumConstType.Type = Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE;
        enumConstType.ByRef = false;
        enumConstType.Pinned = false;

        for (var i = 1; i < il2cppEnum.FieldCount; i++)
        {
            var fieldValue = enumValues.GetValue(i - 1);
            var il2cppField = UnityVersionHandler.Wrap(il2cppFields + i * UnityVersionHandler.FieldInfoSize());
            il2cppField.Name = Marshal.StringToCoTaskMemUTF8(enumNames[i - 1]);
            il2cppField.Type = enumConstType.TypePointer;
            il2cppField.Parent = il2cppEnum.ClassPointer;
            il2cppField.Offset = 0;

            CreateOrUpdateFieldDefaultValue(il2cppField.FieldInfoPointer, elementClass.ThisArg.TypePointer, fieldValue);
        }

        il2cppEnum.Fields = il2cppFields;

        il2cppEnum.TypeHierarchyDepth = (byte)(1 + baseEnum.TypeHierarchyDepth);
        il2cppEnum.TypeHierarchy = (Il2CppClass**)Marshal.AllocHGlobal(il2cppEnum.TypeHierarchyDepth * sizeof(void*));
        for (var i = 0; i < il2cppEnum.TypeHierarchyDepth; i++)
            il2cppEnum.TypeHierarchy[i] = baseEnum.TypeHierarchy[i];
        il2cppEnum.TypeHierarchy[il2cppEnum.TypeHierarchyDepth - 1] = il2cppEnum.ClassPointer;

        RuntimeSpecificsStore.SetClassInfo(il2cppEnum.Pointer, true);
        Il2CppClassPointerStore.SetNativeClassPointer(type, il2cppEnum.Pointer);

        InjectorHelpers.AddTypeToLookup(type, il2cppEnum.Pointer);

        if (logSuccess)
            Logger.Instance.LogInformation("Registered managed enum {Type} in il2cpp domain", type);
    }
}
