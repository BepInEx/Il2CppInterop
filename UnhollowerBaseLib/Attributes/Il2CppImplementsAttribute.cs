using System;

namespace UnhollowerBaseLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class Il2CppImplementsAttribute : Attribute
    {
        public readonly Type[] Interfaces;

        public Il2CppImplementsAttribute(params Type[] interfaces)
        {
            Interfaces = interfaces;
        }
    }
}
