using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class FieldAnalysisContextExtensions
{
    extension(FieldAnalysisContext field)
    {
        [MaybeNull]
        public PropertyAnalysisContext PropertyAccessor
        {
            get => @field.GetExtraData<PropertyAnalysisContext>("PropertyAccessor");
            set => @field.PutExtraData("PropertyAccessor", value);
        }

        [MaybeNull]
        public FieldAnalysisContext FieldInfoAddressStorage
        {
            get => @field.GetExtraData<FieldAnalysisContext>("FieldInfoAddressStorage");
            set => @field.PutExtraData("FieldInfoAddressStorage", value);
        }

        [MaybeNull]
        public FieldAnalysisContext OffsetStorage
        {
            get => @field.GetExtraData<FieldAnalysisContext>("OffsetStorage");
            set => @field.PutExtraData("OffsetStorage", value);
        }

        /// <summary>
        /// Gets the field info address storage for this field, and instantiate for use in this field's declaring type.
        /// </summary>
        /// <returns>The instantiated storage field</returns>
        public FieldAnalysisContext GetInstantiatedFieldInfoAddressStorage()
        {
            return field.GetInstantiatedStorageField(field.FieldInfoAddressStorage);
        }

        public FieldAnalysisContext GetInstantiatedOffsetStorage()
        {
            return field.GetInstantiatedStorageField(field.OffsetStorage);
        }

        private FieldAnalysisContext GetInstantiatedStorageField(FieldAnalysisContext? storageField)
        {
            Debug.Assert(storageField is not null);
            Debug.Assert(storageField.DeclaringType.GenericParameters.Count == field.DeclaringType.GenericParameters.Count);

            if (storageField.DeclaringType.GenericParameters.Count == 0)
            {
                return storageField;
            }
            else
            {
                return storageField.MakeConcreteGeneric(field.DeclaringType.GenericParameters);
            }
        }

        public ConcreteGenericFieldAnalysisContext MakeConcreteGeneric(IEnumerable<TypeAnalysisContext> declaringTypeGenericArguments)
        {
            return new ConcreteGenericFieldAnalysisContext(
                field,
                field.DeclaringType.MakeGenericInstanceType(declaringTypeGenericArguments)
            );
        }

        public FieldAnalysisContext MaybeMakeConcreteGeneric(IEnumerable<TypeAnalysisContext> declaringTypeGenericArguments)
        {
            if (field.DeclaringType.GenericParameters.Count == 0)
                return field;
            else
                return field.MakeConcreteGeneric(declaringTypeGenericArguments);
        }

        public FieldAnalysisContext SelfInstantiateIfNecessary()
        {
            return field.MaybeMakeConcreteGeneric(field.DeclaringType.GenericParameters);
        }
    }
}
