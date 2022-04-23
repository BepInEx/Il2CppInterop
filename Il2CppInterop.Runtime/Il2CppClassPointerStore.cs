using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Attributes;

namespace Il2CppInterop.Runtime
{
    public static class Il2CppClassPointerStore
    {
        public static IntPtr GetNativeClassPointer(Type type)
        {
#if !MINI
            if (type == typeof(void)) return Il2CppClassPointerStore<Il2CppSystem.Void>.NativeClassPtr;
            if (type == typeof(Il2CppSystem.String)) return Il2CppClassPointerStore<string>.NativeClassPtr;
            return (IntPtr)typeof(Il2CppClassPointerStore<>)
                .MakeGenericType(type)
                .GetField(nameof(Il2CppClassPointerStore<int>.NativeClassPtr))
                .GetValue(null);
#else
            return IntPtr.Zero;
#endif
        }

        internal static void SetNativeClassPointer(Type type, IntPtr value)
        {
            typeof(Il2CppClassPointerStore<>)
                .MakeGenericType(type)
                .GetField(nameof(Il2CppClassPointerStore<int>.NativeClassPtr))
                .SetValue(null, value);
        }
    }

    public static class Il2CppClassPointerStore<T>
    {
        public static IntPtr NativeClassPtr;
        public static Type CreatedTypeRedirect;
        
        static Il2CppClassPointerStore()
        {
            var targetType = typeof(T);
            RuntimeHelpers.RunClassConstructor(targetType.TypeHandle);
            if (targetType.IsPrimitive || targetType == typeof(string))
            {
                RuntimeHelpers.RunClassConstructor(AppDomain.CurrentDomain.GetAssemblies()
                    .Single(it => it.GetName().Name == "Il2Cppmscorlib").GetType("Il2Cpp" + targetType.FullName)
                    .TypeHandle);
            }
            
            foreach (var customAttribute in targetType.CustomAttributes)
            {
                if (customAttribute.AttributeType != typeof(AlsoInitializeAttribute)) continue;
                
                var linkedType = (Type) customAttribute.ConstructorArguments[0].Value;
                RuntimeHelpers.RunClassConstructor(linkedType.TypeHandle);
            }
        }
    }
}