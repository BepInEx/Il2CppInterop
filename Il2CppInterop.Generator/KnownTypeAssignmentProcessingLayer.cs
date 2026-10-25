using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public sealed class KnownTypeAssignmentProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Known Type Assignment";
    public override string Id => "known_type_assignment";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var knownType in Enum.GetValues<KnownTypeCode>())
        {
            if (knownType is KnownTypeCode.None or KnownTypeCode.Il2CppSystem_IObject or KnownTypeCode.Il2CppSystem_IValueType or KnownTypeCode.Il2CppSystem_IEnum)
                continue;

            var typeName = knownType.ToString().Replace('_', '.');
            if (typeName.StartsWith("Il2Cpp", StringComparison.Ordinal))
            {
                appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow(typeName).KnownType = knownType;
            }
            else
            {
                appContext.Mscorlib.GetTypeByFullNameOrThrow(typeName).KnownType = knownType;
            }
        }
    }
}
