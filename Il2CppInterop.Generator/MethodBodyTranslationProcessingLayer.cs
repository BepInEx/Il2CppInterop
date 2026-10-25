using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class MethodBodyTranslationProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "method_body_translation";
    public override string Name => "Method Body Translation";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        Logger.InfoNewline("Translating method bodies...", nameof(MethodBodyTranslationProcessingLayer));
        var successfulCount = 0;
        var totalCount = 0;
        foreach (var method in appContext.AllTypes.SelectMany(t => t.Methods))
        {
            if (method.HasExtraData<OriginalMethodBody>())
            {
                totalCount++;
            }
            if (TranslatedMethodBody.TryTranslateOriginalMethodBody(method))
            {
                successfulCount++;
            }
            method.RemoveExtraData<OriginalMethodBody>();
        }

        // Report how many method bodies were successfully translated.
        // This total count should be the same as the count of methods with original bodies that were unstripped earlier.
        Logger.InfoNewline($"Translated the original method body for {successfulCount}/{totalCount} attempts.", nameof(MethodBodyTranslationProcessingLayer));
    }
}
