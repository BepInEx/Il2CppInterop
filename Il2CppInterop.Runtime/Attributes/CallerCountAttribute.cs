using System;

namespace Il2CppInterop.Runtime.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class CallerCountAttribute : Attribute
    {
        public readonly int Count;

        public CallerCountAttribute(int count)
        {
            Count = count;
        }
    }
}