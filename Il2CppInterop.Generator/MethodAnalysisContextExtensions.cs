using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

        public MethodAnalysisContext MostUserFriendlyOverload
        {
            get => method.GetExtraData<MethodAnalysisContext>("MostUserFriendlyOverload") ?? method;
            set => method.PutExtraData("MostUserFriendlyOverload", value);
        }

        /// <summary>
        /// The unsafe invoke method for this method.
        /// </summary>
        [MaybeNull]
        public MethodAnalysisContext UnsafeInvokeMethod
        {
            get => method.GetExtraData<MethodAnalysisContext>("UnsafeInvokeMethod");
            set => method.PutExtraData("UnsafeInvokeMethod", value);
        }

        /// <summary>
        /// The unsafe implementation method for this method.
        /// </summary>
        [MaybeNull]
        public MethodAnalysisContext UnsafeImplementationMethod
        {
            get => method.GetExtraData<MethodAnalysisContext>("UnsafeImplementationMethod");
            set => method.PutExtraData("UnsafeImplementationMethod", value);
        }

        /// <summary>
        /// The interface method that should be used instead when emitting calls to this method during unstripping.
        /// </summary>
        [MaybeNull]
        public MethodAnalysisContext InterfaceRedirectMethod
        {
            get => method.GetExtraData<MethodAnalysisContext>("InterfaceRedirectMethod");
            set => method.PutExtraData("InterfaceRedirectMethod", value);
        }

        public bool IsInstanceConstructor => method.Name == ".ctor";
        public bool IsStaticConstructor => method.Name == ".cctor";
        public bool IsConstructor => method.IsInstanceConstructor || method.IsStaticConstructor;
        public bool IsPublic => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
        public bool IsSpecialName => (method.Attributes & MethodAttributes.SpecialName) != default;

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

        public ConcreteGenericMethodAnalysisContext MakeConcreteGeneric(IEnumerable<TypeAnalysisContext> typeArguments, IEnumerable<TypeAnalysisContext> methodArguments)
        {
            return new ConcreteGenericMethodAnalysisContext(method, typeArguments, methodArguments);
        }

        public MethodAnalysisContext MaybeMakeConcreteGeneric(IReadOnlyCollection<TypeAnalysisContext> typeArguments, IReadOnlyCollection<TypeAnalysisContext> methodArguments)
        {
            if (typeArguments.Count == 0 && methodArguments.Count == 0)
                return method;
            return method.MakeConcreteGeneric(typeArguments, methodArguments);
        }
    }
}
