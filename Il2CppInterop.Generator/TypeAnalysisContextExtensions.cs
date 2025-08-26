using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

internal static class TypeAnalysisContextExtensions
{
    extension(TypeAnalysisContext type)
    {
        public bool IsModuleType => type.DeclaringType is null && string.IsNullOrEmpty(type.DefaultNamespace) && type.DefaultName == "<Module>";
        public bool IsPrivateImplementationDetailsType => type.DeclaringType is null && string.IsNullOrEmpty(type.DefaultNamespace) && type.DefaultName == "<PrivateImplementationDetails>";
        public bool HasGenericParameters => type.GenericParameters.Count > 0;

        [MaybeNull]
        public Type SourceType
        {
            get => type.GetExtraData<Type>("SourceType");
            set => type.PutExtraData("SourceType", value);
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

        public MethodAnalysisContext GetMethodByName(string name)
        {
            for (var i = type.Methods.Count - 1; i >= 0; i--)
            {
                var method = type.Methods[i];
                if (method.Name == name)
                {
                    return method;
                }
            }
            throw new Exception($"Method {name} not found in type {type.Name}");
        }

        public FieldAnalysisContext GetFieldByName(string? name)
        {
            for (var i = type.Fields.Count - 1; i >= 0; i--)
            {
                var field = type.Fields[i];
                if (field.Name == name)
                {
                    return field;
                }
            }
            throw new Exception($"Field {name} not found in type {type.Name}");
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

        public bool TryGetPropertyByName(string? name, [NotNullWhen(true)] out PropertyAnalysisContext? property)
        {
            for (var i = type.Properties.Count - 1; i >= 0; i--)
            {
                var prop = type.Properties[i];
                if (prop.Name == name)
                {
                    property = prop;
                    return true;
                }
            }
            property = null;
            return false;
        }

        public MethodAnalysisContext GetImplicitConversionFrom(TypeAnalysisContext sourceType)
        {
            return GetConversion("op_Implicit", type, sourceType, type);
        }

        public MethodAnalysisContext GetImplicitConversionTo(TypeAnalysisContext targetType)
        {
            return GetConversion("op_Implicit", type, type, targetType);
        }

        public MethodAnalysisContext GetExplicitConversionFrom(TypeAnalysisContext sourceType)
        {
            return GetConversion("op_Explicit", type, sourceType, type);
        }

        public MethodAnalysisContext GetExplicitConversionTo(TypeAnalysisContext targetType)
        {
            return GetConversion("op_Explicit", type, type, targetType);
        }

        public TypeAnalysisContext SelfInstantiateIfGeneric()
        {
            if (type.GenericParameters.Count == 0)
            {
                return type;
            }
            else
            {
                return type.MakeGenericInstanceType(type.GenericParameters);
            }
        }
    }

    private static MethodAnalysisContext GetConversion([ConstantExpected] string name, TypeAnalysisContext declaringType, TypeAnalysisContext sourceType, TypeAnalysisContext targetType)
    {
        return declaringType.Methods.First(m =>
        {
            return m.Name == name && m.IsStatic && m.ReturnType == targetType && m.Parameters.Count == 1 && m.Parameters[0].ParameterType == sourceType;
        });
    }
}
