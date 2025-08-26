using System.Runtime.CompilerServices;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class InjectedMethodAnalysisContextExtensions
{
    public static void SetDefaultReturnType(this InjectedMethodAnalysisContext context, TypeAnalysisContext type)
    {
        GetDefaultReturnType(context) = type;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = $"<{nameof(InjectedMethodAnalysisContext.DefaultReturnType)}>k__BackingField")]
    private static extern ref TypeAnalysisContext GetDefaultReturnType(InjectedMethodAnalysisContext context);
}
