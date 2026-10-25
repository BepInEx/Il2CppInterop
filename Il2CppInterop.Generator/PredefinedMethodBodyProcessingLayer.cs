using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class PredefinedMethodBodyProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Predefined Method Body Processor";
    public override string Id => "predefined_method_body_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference));
        var byReference_GetValue = byReference.GetMethodByName(nameof(ByReference.GetValue));

        var generationInternals = appContext.ResolveTypeOrThrow(typeof(GenerationInternals));

        // Il2Cpp compiles Interlocked.CompareExchange<T> and Interlocked.Exchange<T> inline at each call, so their generic instances have no code to invoke.
        var interlocked = appContext.Il2CppMscorlib.GetTypeByFullName("Il2CppSystem.Threading.Interlocked");
        if (interlocked is not null)
        {
            foreach (var method in interlocked.Methods)
            {
                if (method.IsInjected || method.GenericParameters.Count != 1)
                    continue;

                var replacement = method.DefaultName switch
                {
                    "CompareExchange" => HasObjectOverload(interlocked, "CompareExchange", 3)
                        ? nameof(GenerationInternals.InterlockedCompareExchangeWithObjectSupport)
                        : nameof(GenerationInternals.InterlockedCompareExchangeWithoutObjectSupport),
                    "Exchange" => HasObjectOverload(interlocked, "Exchange", 2)
                        ? nameof(GenerationInternals.InterlockedExchangeWithObjectSupport)
                        : nameof(GenerationInternals.InterlockedExchangeWithoutObjectSupport),
                    _ => null,
                };
                if (replacement is not null)
                    ReplaceImplementation(method, generationInternals.GetMethodByName(replacement), byReference_GetValue);
            }
        }
    }

    /// <summary>
    /// Whether the game's build includes the overload of <paramref name="name"/> for object, such as <c>CompareExchange(ref object, object, object)</c>.
    /// </summary>
    private static bool HasObjectOverload(TypeAnalysisContext interlocked, string name, int parameterCount)
    {
        return interlocked.Methods.Any(method => method.DefaultName == name
            && !method.IsInjected
            && !method.IsUnstripped
            && method.GenericParameters.Count == 0
            && method.Parameters.Count == parameterCount
            && method.ReturnType.KnownType == KnownTypeCode.Il2CppSystem_IObject
            && method.Parameters[0] is { DefaultParameterType: ByRefTypeAnalysisContext, ParameterType: GenericInstanceTypeAnalysisContext { GenericArguments.Count: 1 } byReference }
            && byReference.GenericArguments[0].KnownType == KnownTypeCode.Il2CppSystem_IObject
            && method.Parameters.Skip(1).All(parameter => parameter.ParameterType.KnownType == KnownTypeCode.Il2CppSystem_IObject));
    }

    /// <summary>
    /// Makes the unsafe implementation of <paramref name="method"/> call <paramref name="replacement"/> with the values of its parameters.
    /// </summary>
    private static void ReplaceImplementation(MethodAnalysisContext method, MethodAnalysisContext replacement, MethodAnalysisContext byReference_GetValue)
    {
        var implementation = method.UnsafeImplementationMethod;
        if (implementation is null)
            return;

        List<Instruction> instructions = new();
        foreach (var parameter in implementation.Parameters)
        {
            var dataType = ((GenericInstanceTypeAnalysisContext)parameter.ParameterType).GenericArguments[0];
            instructions.Add(CilOpCodes.Ldarg, parameter);
            instructions.Add(CilOpCodes.Call, byReference_GetValue.MakeGenericInstanceMethod(dataType));
        }
        instructions.Add(CilOpCodes.Call, replacement.MakeGenericInstanceMethod([.. implementation.GenericParameters]));
        instructions.Add(CilOpCodes.Ret);

        implementation.RemoveExtraData<TranslatedMethodBody>();
        implementation.PutExtraData(new NativeMethodBody()
        {
            Instructions = instructions,
        });
    }
}
