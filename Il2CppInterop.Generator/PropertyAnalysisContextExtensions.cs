using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class PropertyAnalysisContextExtensions
{
    extension(PropertyAnalysisContext property)
    {
        [MaybeNull]
        public FieldAnalysisContext OriginalField
        {
            get => property.GetExtraData<FieldAnalysisContext>("OriginalField");
            set => property.PutExtraData("OriginalField", value);
        }
    }
}
