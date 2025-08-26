using System.Runtime.CompilerServices;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class InjectedParameterAnalysisContextExtensions
{
    public static void SetDefaultParameterType(this InjectedParameterAnalysisContext context, TypeAnalysisContext type)
    {
        GetDefaultParameterType(context) = type;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = $"<{nameof(InjectedParameterAnalysisContext.DefaultParameterType)}>k__BackingField")]
    private static extern ref TypeAnalysisContext GetDefaultParameterType(InjectedParameterAnalysisContext context);
}
