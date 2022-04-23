using System;

namespace Il2CppInterop.Runtime.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class Il2CppImplementsAttribute : Attribute
    {
        public Type[] Interfaces { get; }

        public Il2CppImplementsAttribute(params Type[] interfaces)
        {
            Interfaces = interfaces;
        }
    }
}
