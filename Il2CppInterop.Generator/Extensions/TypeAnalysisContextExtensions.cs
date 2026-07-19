using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator.Extensions;

internal static class TypeAnalysisContextExtensions
{
    extension(TypeAnalysisContext type)
    {
        public bool IsModuleType => type.DeclaringType is null && string.IsNullOrEmpty(type.DefaultNamespace) && type.DefaultName == "<Module>";
        public bool IsPrivateImplementationDetailsType => type.DeclaringType is null && string.IsNullOrEmpty(type.DefaultNamespace) && type.DefaultName == "<PrivateImplementationDetails>";
        public bool HasGenericParameters => type.GenericParameters.Count > 0;
        public bool IsIl2CppPrimitive
        {
            get
            {
                if (type is ReferencedTypeAnalysisContext)
                    return false;

                if (type.Namespace != "Il2CppSystem")
                    return false;

                if (type.DeclaringAssembly.Name != "Il2Cppmscorlib")
                    return false;

                return type.Name is
                    "Boolean" or
                    "Byte" or
                    "SByte" or
                    "Int16" or
                    "UInt16" or
                    "Int32" or
                    "UInt32" or
                    "Int64" or
                    "UInt64" or
                    "Single" or
                    "Double" or
                    "Char" or
                    "IntPtr" or
                    "UIntPtr" or
                    "Void";
            }
        }

        public bool IsIl2CppDelegate => type is { BaseType: { Namespace: "Il2CppSystem", Name: "MulticastDelegate" } } or GenericInstanceTypeAnalysisContext { GenericType.BaseType: { Namespace: "Il2CppSystem", Name: "MulticastDelegate" } };

        /// <summary>
        /// The fields, methods, properties, and events of this type.
        /// </summary>
        public IEnumerable<HasCustomAttributesAndName> Members
        {
            get
            {
                return ((IEnumerable<HasCustomAttributesAndName>)type.Fields).Concat(type.Methods).Concat(type.Properties).Concat(type.Events);
            }
        }

        public string CSharpName
        {
            get
            {
                if (GenericTypeName.TryMatch(type.Name, out var result, out _))
                {
                    return result;
                }
                return type.Name;
            }
        }

        [MaybeNull]
        public MethodAnalysisContext PointerConstructor
        {
            get => type.GetExtraData<MethodAnalysisContext>("PointerConstructor");
            set => type.PutExtraData("PointerConstructor", value);
        }

        [MaybeNull]
        public TypeAnalysisContext InitializationType
        {
            get => type.GetExtraData<TypeAnalysisContext>("InitializationType");
            set => type.PutExtraData("InitializationType", value);
        }

        [MaybeNull]
        public FieldAnalysisContext SizeStorage
        {
            get => type.GetExtraData<FieldAnalysisContext>("SizeStorage");
            set => type.PutExtraData("SizeStorage", value);
        }

        [MaybeNull]
        public TypeAnalysisContext EnumIl2CppUnderlyingType
        {
            get => type.GetExtraData<TypeAnalysisContext>("EnumIl2CppUnderlyingType");
            set => type.PutExtraData("EnumIl2CppUnderlyingType", value);
        }

        [MaybeNull]
        public TypeAnalysisContext EnumMonoUnderlyingType
        {
            get => type.GetExtraData<TypeAnalysisContext>("EnumMonoUnderlyingType");
            set => type.PutExtraData("EnumMonoUnderlyingType", value);
        }

        [MaybeNull]
        public InjectedTypeAnalysisContext SystemExceptionType
        {
            get => type.GetExtraData<InjectedTypeAnalysisContext>("SystemExceptionType");
            set => type.PutExtraData("SystemExceptionType", value);
        }

        [MaybeNull]
        public List<Instruction> StaticConstructorInstructions
        {
            get => type.GetExtraData<List<Instruction>>("StaticConstructorInstructions");
            set => type.PutExtraData("StaticConstructorInstructions", value);
        }

        [MaybeNull]
        public TypeAnalysisContext ManagedDelegateType
        {
            get => type.GetExtraData<TypeAnalysisContext>("ManagedDelegateType");
            set => type.PutExtraData("ManagedDelegateType", value);
        }

        public KnownTypeCode KnownType
        {
            get => type.GetExtraStruct("KnownType", KnownTypeCode.None);
            set => type.PutExtraStruct("KnownType", value);
        }

        public List<Instruction> GetOrCreateStaticConstructorInstructions()
        {
            var instructions = type.StaticConstructorInstructions;
            if (instructions is null)
            {
                instructions = [];
                type.StaticConstructorInstructions = instructions;
            }
            return instructions;
        }

        public MethodAnalysisContext? TryGetMethodByName(string name)
        {
            for (var i = type.Methods.Count - 1; i >= 0; i--)
            {
                var method = type.Methods[i];
                if (method.Name == name)
                {
                    return method;
                }
            }
            return null;
        }

        public MethodAnalysisContext GetMethodByName(string name)
        {
            return type.TryGetMethodByName(name) ?? throw new Exception($"Method {name} not found in type {type.Name}");
        }

        public FieldAnalysisContext GetFieldByName(string? name)
        {
            return type.TryGetFieldByName(name) ?? throw new Exception($"Field {name} not found in type {type.Name}");
        }

        public FieldAnalysisContext? TryGetFieldByName(string? name)
        {
            for (var i = type.Fields.Count - 1; i >= 0; i--)
            {
                var field = type.Fields[i];
                if (field.Name == name)
                {
                    return field;
                }
            }
            return null;
        }

        public bool TryGetFieldByName(string? name, [NotNullWhen(true)] out FieldAnalysisContext? field)
        {
            field = type.TryGetFieldByName(name);
            return field is not null;
        }

        public PropertyAnalysisContext GetPropertyByName(string? name)
        {
            return type.TryGetPropertyByName(name) ?? throw new Exception($"Property {name} not found in type {type.Name}");
        }

        public PropertyAnalysisContext? TryGetPropertyByName(string? name)
        {
            for (var i = type.Properties.Count - 1; i >= 0; i--)
            {
                var property = type.Properties[i];
                if (property.Name == name)
                {
                    return property;
                }
            }
            return null;
        }

        public bool TryGetPropertyByName(string? name, [NotNullWhen(true)] out PropertyAnalysisContext? property)
        {
            property = type.TryGetPropertyByName(name);
            return property is not null;
        }

        public bool TryGetMethodInSlot(int slot, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            if (type is GenericInstanceTypeAnalysisContext genericInstanceType)
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
                var baseMethod = type.Methods.FirstOrDefault(m => m.Slot == slot);
                if (baseMethod is not null)
                {
                    method = baseMethod;
                    return true;
                }
            }

            method = null;
            return false;
        }

        public MethodAnalysisContext GetImplicitConversionFrom(TypeAnalysisContext sourceType)
        {
            return type.TryGetImplicitConversionFrom(sourceType, out var method)
                ? method
                : throw new Exception($"No implicit conversion from {sourceType.Name} to {type.Name} found.");
        }

        public MethodAnalysisContext GetImplicitConversionTo(TypeAnalysisContext targetType)
        {
            return type.TryGetImplicitConversionTo(targetType, out var method)
                ? method
                : throw new Exception($"No implicit conversion from {type.Name} to {targetType.Name} found.");
        }

        public MethodAnalysisContext GetExplicitConversionFrom(TypeAnalysisContext sourceType)
        {
            return type.TryGetExplicitConversionFrom(sourceType, out var method)
                ? method
                : throw new Exception($"No explicit conversion from {sourceType.Name} to {type.Name} found.");
        }

        public MethodAnalysisContext GetExplicitConversionTo(TypeAnalysisContext targetType)
        {
            return type.TryGetExplicitConversionTo(targetType, out var method)
                ? method
                : throw new Exception($"No explicit conversion from {type.Name} to {targetType.Name} found.");
        }

        public bool TryGetImplicitConversionFrom(TypeAnalysisContext sourceType, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            method = GetConversion("op_Implicit", type, sourceType, type.SelfInstantiateIfGeneric());
            return method is not null;
        }

        public bool TryGetImplicitConversionTo(TypeAnalysisContext targetType, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            method = GetConversion("op_Implicit", type, type.SelfInstantiateIfGeneric(), targetType);
            return method is not null;
        }

        public bool TryGetExplicitConversionFrom(TypeAnalysisContext sourceType, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            method = GetConversion("op_Explicit", type, sourceType, type.SelfInstantiateIfGeneric());
            return method is not null;
        }

        public bool TryGetExplicitConversionTo(TypeAnalysisContext targetType, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            method = GetConversion("op_Explicit", type, type.SelfInstantiateIfGeneric(), targetType);
            return method is not null;
        }

        public bool TryGetConversionFrom(TypeAnalysisContext sourceType, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            return type.TryGetImplicitConversionFrom(sourceType, out method) || type.TryGetExplicitConversionFrom(sourceType, out method);
        }

        public bool TryGetConversionTo(TypeAnalysisContext targetType, [NotNullWhen(true)] out MethodAnalysisContext? method)
        {
            return type.TryGetImplicitConversionTo(targetType, out method) || type.TryGetExplicitConversionTo(targetType, out method);
        }

        public TypeAnalysisContext MaybeMakeGenericInstanceType(IReadOnlyCollection<TypeAnalysisContext> genericArguments)
        {
            if (type.GenericParameters.Count == 0)
            {
                return type;
            }
            else
            {
                return type.MakeGenericInstanceType(genericArguments);
            }
        }

        public TypeAnalysisContext SelfInstantiateIfGeneric() => type.MaybeMakeGenericInstanceType(type.GenericParameters);

        public bool ImplementsInterface(TypeAnalysisContext interfaceType)
        {
            foreach (var implementedInterface in type.InterfaceContexts)
            {
                if (TypeAnalysisContextEqualityComparer.Instance.Equals(implementedInterface, interfaceType))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static MethodAnalysisContext? GetConversion([ConstantExpected] string name, TypeAnalysisContext declaringType, TypeAnalysisContext sourceType, TypeAnalysisContext targetType)
    {
        return declaringType.Methods.FirstOrDefault(m =>
        {
            return m.Name == name
                && m.IsStatic
                && m.Parameters.Count == 1
                && TypeAnalysisContextEqualityComparer.Instance.Equals(m.ReturnType, targetType)
                && TypeAnalysisContextEqualityComparer.Instance.Equals(m.Parameters[0].ParameterType, sourceType);
        });
    }
}
