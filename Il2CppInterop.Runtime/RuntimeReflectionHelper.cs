using System;
#if !MINI
using Il2CppSystem.Reflection;
using Il2CppInterop.Runtime;
using RuntimeTypeHandle = Il2CppSystem.RuntimeTypeHandle;
using Type = Il2CppSystem.Type;
#endif

namespace Il2CppInterop.Runtime
{
    public static class RuntimeReflectionHelper
    {
        public static IntPtr GetNestedTypeViaReflection(IntPtr enclosingClass, string nestedTypeName)
        {
#if  !MINI
            var reflectionType = Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(enclosingClass));
            var nestedType = reflectionType.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);

            return nestedType != null ? IL2CPP.il2cpp_class_from_system_type(nestedType.Pointer) : IntPtr.Zero;
#else
            throw new NotImplementedException();
#endif
        }

        public static RuntimeTypeHandle GetRuntimeTypeHandle<T>()
        {
#if !MINI
            return Il2CppType.Of<T>().TypeHandle;
#else
            throw new NotImplementedException();
#endif
        }
    }
}