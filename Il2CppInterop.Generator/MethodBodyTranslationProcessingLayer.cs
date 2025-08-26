using Cpp2IL.Core.Api;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class MethodBodyTranslationProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "method_body_translation";
    public override string Name => "Method Body Translation";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var successfulCount = 0;
        var totalCount = 0;
        foreach (var method in appContext.AllTypes.SelectMany(t => t.Methods))
        {
            var successful = TranslatedMethodBody.MaybeStoreTranslatedMethodBody(method);
            if (successful)
            {
                successfulCount++;
            }
            if (method.HasExtraData<OriginalMethodBody>())
            {
                totalCount++;
            }
        }

        // Report how many method bodies were successfully translated.
        // Note: this total count is less than the count of methods with original bodies that were unstripped earlier.
        // This is because static constructors were removed.
        Logger.InfoNewline($"Translated the original method body for {successfulCount}/{totalCount} attempts.", nameof(MethodBodyTranslationProcessingLayer));
    }
}
