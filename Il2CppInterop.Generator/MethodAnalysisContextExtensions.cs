using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class MethodAnalysisContextExtensions
{
    extension(MethodAnalysisContext method)
    {
        /// <summary>
        /// This is a runtime-implemented method. In other words, it should not have a method body because the runtime provides its own implementation.
        /// </summary>
        /// <remarks>
        /// One example of this is in delegate invocation methods.
        /// </remarks>
        public bool RuntimeImplemented
        {
            get => method.GetExtraData<object>("RuntimeImplemented") is true;
            set => method.PutExtraData<object>("RuntimeImplemented", value);
        }

        [MaybeNull]
        public FieldAnalysisContext MethodInfoField
        {
            get => method.GetExtraData<FieldAnalysisContext>("MethodInfoField");
            set => method.PutExtraData("MethodInfoField", value);
        }

        public bool IsInstanceConstructor => method.Name == ".ctor";
        public bool IsStaticConstructor => method.Name == ".cctor";
        public bool IsConstructor => method.IsInstanceConstructor || method.IsStaticConstructor;

        public bool ImplementsAnInterfaceMethod
        {
            get
            {
                var count = 0;
                foreach (var x in method.Overrides)
                {
                    count++;
                    if (count > 1)
                    {
                        return true;
                    }
                }
                return count == 1 && method.BaseMethod is null;
            }
        }

        public FieldAnalysisContext GetInstantiatedMethodInfoField()
        {
            var methodInfoField = method.MethodInfoField;
            Debug.Assert(methodInfoField is not null);
            Debug.Assert(method.DeclaringType is not null);

            IReadOnlyList<TypeAnalysisContext> methodInfoGenericArguments = [.. method.DeclaringType.GenericParameters, .. method.GenericParameters];
            if (methodInfoGenericArguments.Count == 0)
            {
                return methodInfoField;
            }
            else
            {
                return methodInfoField.MakeConcreteGeneric(methodInfoGenericArguments);
            }
        }
    }
}
