using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;
using LibCpp2IL;

namespace Il2CppInterop.Generator.Extensions;

internal static class MethodAnalysisContextExtensions
{
    extension(MethodAnalysisContext method)
    {
        [MaybeNull]
        public FieldAnalysisContext MethodInfoField
        {
            get => method.GetExtraData<FieldAnalysisContext>("MethodInfoField");
            set => method.PutExtraData("MethodInfoField", value);
        }

        [MaybeNull]
        public FieldAnalysisContext ICallDelegateField
        {
            get => method.GetExtraData<FieldAnalysisContext>("ICallDelegateField");
            set => method.PutExtraData("ICallDelegateField", value);
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

        [MaybeNull]
        public int InitializationClassIndex
        {
            get => method.GetExtraStruct("InitializationClassIndex", -1);
            set => method.PutExtraStruct("InitializationClassIndex", value);
        }

        public MethodAnalysisContext? UltimateBaseMethod
        {
            get
            {
                var currentBaseMethod = method.BaseMethodFixed;
                if (currentBaseMethod is null)
                    return null;
                MethodAnalysisContext? nextBaseMethod;
                while (true)
                {
                    nextBaseMethod = currentBaseMethod.BaseMethodFixed;
                    if (nextBaseMethod is not null)
                    {
                        currentBaseMethod = nextBaseMethod;
                    }
                    else
                    {
                        break;
                    }
                }
                return currentBaseMethod;
            }
        }

        public MethodAnalysisContext? BaseMethodFixed
        {
            get
            {
                if (method.Definition == null)
                    return null;

                var vtable = method.DeclaringType?.Definition?.VTable;
                if (vtable == null)
                    return null;

                for (var i = 0; i < vtable.Length; ++i)
                {
                    var vtableEntry = vtable[i];
                    if (vtableEntry is null or { Type: not MetadataUsageType.MethodDef } || vtableEntry.AsMethod() != method.Definition)
                        continue;

                    if (IsInterfaceSlot(method, i))
                    {
                        continue;
                    }

                    var baseType = method.DeclaringType?.DefaultBaseType;
                    while (baseType is not null)
                    {
                        if (TryGetMethodForSlot(baseType, i, out var method2))
                        {
                            return method2;
                        }
                        baseType = baseType.DefaultBaseType;
                    }
                }
                return null;
            }
        }

        public bool IsInstanceConstructor => method.Name == ".ctor";
        public bool IsStaticConstructor => method.Name == ".cctor";
        public bool IsConstructor => method.IsInstanceConstructor || method.IsStaticConstructor;
        public bool IsPublic => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
        public bool IsSpecialName => (method.Attributes & MethodAttributes.SpecialName) != default;
        public bool IsFinal => (method.Attributes & MethodAttributes.Final) != default;

        public bool ImplementsAnInterfaceMethod => method.Overrides.Count > 0;

        public ushort Slot => method.Definition?.slot ?? ushort.MaxValue;

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

    private static bool IsInterfaceSlot(MethodAnalysisContext method, int slot)
    {
        // Interface inheritance
        foreach (var interfaceOffset in method.DeclaringType!.Definition!.InterfaceOffsets)
        {
            if (slot >= interfaceOffset.offset)
            {
                var interfaceTypeContext = interfaceOffset.Type.ToContext(method.AppContext);
                if (interfaceTypeContext != null && TryGetMethodForSlot(interfaceTypeContext, slot - interfaceOffset.offset, out _))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryGetMethodForSlot(TypeAnalysisContext declaringType, int slot, [NotNullWhen(true)] out MethodAnalysisContext? method)
    {
        if (declaringType is GenericInstanceTypeAnalysisContext genericInstanceType)
        {
            var genericMethod = genericInstanceType.GenericType.Methods.FirstOrDefault(m => m.Slot == slot);
            if (genericMethod is not null)
            {
                method = new ConcreteGenericMethodAnalysisContext(genericMethod, genericInstanceType.GenericArguments, []);
                return true;
            }
        }
        else
        {
            var baseMethod = declaringType.Methods.FirstOrDefault(m => m.Slot == slot);
            if (baseMethod is not null)
            {
                method = baseMethod;
                return true;
            }
        }

        method = null;
        return false;
    }
}
