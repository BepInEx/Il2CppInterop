using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Structs;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;

namespace Il2CppInterop.Runtime.Extensions;

internal static class INativeMethodInfoStructExtensions
{
    extension(INativeMethodInfoStruct methodInfo)
    {
        public unsafe ReadOnlySpan<byte> GetNameSpan()
        {
            var namePtr = methodInfo.Name;
            if (namePtr == IntPtr.Zero)
                return default;

            // Find null terminator
            var length = 0;
            while (Marshal.ReadByte(namePtr, length) > 0)
            {
                length++;
            }
            return new ReadOnlySpan<byte>((void*)namePtr, length);
        }

        [RequiresUnreferencedCode("")]
        [RequiresDynamicCode("")]
        [return: NotNullIfNotNull(nameof(generatedMethod))]
        public static unsafe INativeMethodInfoStruct? FromGeneratedMethod(MethodBase? generatedMethod)
        {
            if (generatedMethod is null)
                return null;

            var methodInfoPointerField = Il2CppInternalsAccess.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(generatedMethod)
                ?? throw new ArgumentException($"Couldn't find the generated method info pointer for {generatedMethod.Name}");

            // Il2CppClassPointerStore calls the static constructor for the type
            Il2CppType.GetClassPointer(generatedMethod.DeclaringType!);

            var methodInfoPointer = (IntPtr)methodInfoPointerField.GetValue(null)!;
            if (methodInfoPointer == IntPtr.Zero)
                throw new ArgumentException($"Generated method info pointer for {generatedMethod.Name} doesn't point to any il2cpp method info");

            return UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfoPointer);
        }
    }
}
