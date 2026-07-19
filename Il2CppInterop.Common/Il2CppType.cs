using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.Attributes;

namespace Il2CppInterop.Common;

public static class Il2CppType
{
    private static readonly nint Il2CppSystemVoidClassPointer = GetIl2CppSystemVoidClassPointer();

    private static unsafe nint GetIl2CppSystemVoidClassPointer()
    {
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == nint.Zero)
        {
            return nint.Zero;
        }

        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, out var assembliesCount);
        for (var i = 0; i < assembliesCount; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            var imageName = IL2CPP.il2cpp_image_get_name(image);
            if (imageName == "mscorlib.dll")
            {
                return IL2CPP.il2cpp_class_from_name(image, "System", "Void");
            }
        }
        return nint.Zero;
    }

    public static int SizeOf<T>() where T : IIl2CppType<T>
    {
        return T.Size;
    }

    public static unsafe void InitializeObject<T>(void* ptr) where T : IIl2CppType<T>
    {
        default(T).WriteToPointer(ptr);
    }

    public static unsafe void CopyObject<T>(void* source, void* destination) where T : IIl2CppType<T>
    {
        Buffer.MemoryCopy(source, destination, T.Size, T.Size);
    }

    public static unsafe void StoreObject<T>(void* ptr, T? value) where T : IIl2CppType<T>
    {
        value.WriteToPointer(ptr);
    }

    /// <summary>
    /// The file name of the assembly
    /// </summary>
    /// <remarks>
    /// Note: this is the image name, not the assembly name.
    /// In practice, the assembly name has no file extension, whereas the image name does.
    /// </remarks>
    public static string GetImageName(Assembly assembly)
    {
        var name = assembly.GetCustomAttribute<Il2CppAssemblyAttribute>()?.Name;
        if (string.IsNullOrEmpty(name))
            name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
            return "Assembly-CSharp.dll"; // This line should be unreachable, but just in case, we return the default assembly name.

        return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.dll";
    }

    /// <summary>
    /// The file name of the assembly that the type is defined in
    /// </summary>
    /// <remarks>
    /// Note: this is the image name, not the assembly name.
    /// In practice, the assembly name has no file extension, whereas the image name does.
    /// </remarks>
    public static string GetImageName(Type type)
    {
        return GetImageName(type.Assembly);
    }

    public static string GetNamespace(Type type)
    {
        return type.GetCustomAttribute<Il2CppTypeAttribute>()?.Namespace ?? type.Namespace ?? "";
    }

    public static string GetName(Type type)
    {
        return type.GetCustomAttribute<Il2CppTypeAttribute>()?.Name ?? type.Name;
    }

    public static void WriteToSpan<T>(this T? value, Span<byte> span) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, span);
    }

    public static T? ReadFromSpan<T>(ReadOnlySpan<byte> span) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(span);
    }

    public static void WriteToSpanAtOffset<T>(this T? value, Span<byte> span, int offset) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, span.Slice(offset, T.Size));
    }

    public static T? ReadFromSpanAtOffset<T>(ReadOnlySpan<byte> span, int offset) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(span.Slice(offset, T.Size));
    }

    public static void WriteToSpanBlittable<T>(T value, Span<byte> span) where T : unmanaged
    {
        MemoryMarshal.Write(span, in value);
    }

    public static T ReadFromSpanBlittable<T>(ReadOnlySpan<byte> span) where T : unmanaged
    {
        return MemoryMarshal.Read<T>(span);
    }

    public static unsafe void WriteToPointer<T>(this T? value, void* ptr) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, new Span<byte>(ptr, T.Size));
    }

    public static unsafe T? ReadFromPointer<T>(void* ptr) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(new ReadOnlySpan<byte>(ptr, T.Size));
    }

    public static T? ReadReference<T>(ReadOnlySpan<byte> span) where T : IIl2CppType<T>
    {
        return T.Unbox(Il2CppObjectPool.Get(ReadPointer(span)));
    }

    public static void WriteReference<T>(T? value, Span<byte> span) where T : IIl2CppType<T>
    {
        var objectPointer = value?.BoxNative() ?? ObjectPointer.Null;
        WritePointer((nint)objectPointer, span);
    }

    public static nint ReadPointer(ReadOnlySpan<byte> span)
    {
        if (BitConverter.IsLittleEndian)
        {
            return BinaryPrimitives.ReadIntPtrLittleEndian(span);
        }
        else
        {
            return BinaryPrimitives.ReadIntPtrBigEndian(span);
        }
    }

    public static void WritePointer(nint pointer, Span<byte> span)
    {
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.WriteIntPtrLittleEndian(span, pointer);
        }
        else
        {
            BinaryPrimitives.WriteIntPtrBigEndian(span, pointer);
        }
    }

    [RequiresDynamicCode("")]
    public static nint GetClassPointer(Type type)
    {
        if (type == typeof(void))
            return Il2CppSystemVoidClassPointer;

        return (nint)typeof(Il2CppType)
            .GetMethod(nameof(GetClassPointer), 1, [])!
            .MakeGenericMethod(type)
            .Invoke(null, null)!;
    }

    [SuppressMessage("Trimming", "IL2059:The type passed to the RunClassConstructor is not statically known, Trimmer can't make sure that its static constructor is available.", Justification = "This will never be called in a trimmed context.")]
    public static nint GetClassPointer<T>() where T : IIl2CppType<T>
    {
        RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        return Il2CppClassPointerStore<T>.NativeClassPointer;
    }

    [RequiresDynamicCode("")]
    public static void SetClassPointer(Type type, nint classPointer)
    {
        typeof(Il2CppType)
            .GetMethod(nameof(SetClassPointer), 1, [typeof(nint)])!
            .MakeGenericMethod(type)
            .Invoke(null, [classPointer]);
    }

    [SuppressMessage("Trimming", "IL2059:The type passed to the RunClassConstructor is not statically known, Trimmer can't make sure that its static constructor is available.", Justification = "This will never be called in a trimmed context.")]
    public static void SetClassPointer<T>(nint classPointer) where T : IIl2CppType<T>
    {
        RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        if (Il2CppClassPointerStore<T>.NativeClassPointer != classPointer)
        {
            if (Il2CppClassPointerStore<T>.NativeClassPointer == nint.Zero)
            {
                Il2CppClassPointerStore<T>.NativeClassPointer = classPointer;
                if (!typeof(T).IsAbstract && !typeof(T).IsInterface)
                {
                    Il2CppObjectPool.RegisterInitializer<T>();
                }
            }
            else
            {
                throw new InvalidOperationException($"Class pointer for type {typeof(T).FullName} has already been set.");
            }
        }
    }

    private static class Il2CppClassPointerStore<T> where T : IIl2CppType<T>
    {
        public static nint NativeClassPointer;
    }
}
