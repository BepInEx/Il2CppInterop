using System.Diagnostics;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class NativeMethodBodyProcessingLayer : Cpp2IlProcessingLayer
{
    private const int ParameterOffset = 2;
    public override string Name => "Native Method Body Processor";
    public override string Id => "native_method_body_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var runtimeInvokeHelper = appContext.ResolveTypeOrThrow(typeof(RuntimeInvokeHelper));
        var invokeAction = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.InvokeAction));
        var invokeFunction = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.InvokeFunction));
        var getPointerForThis = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.GetPointerForThis));
        var getPointerForParameter = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.GetPointerForParameter));

        var intptrPointerType = appContext.SystemTypes.SystemIntPtrType.MakePointerType();

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected || type.IsUnstripped)
                    continue;
                foreach (var method in type.Methods)
                {
                    if (method.IsInjected || method.IsUnstripped)
                        continue;

                    var implementation = method.UnsafeImplementationMethod;
                    Debug.Assert(implementation is not null);
                    Debug.Assert(implementation.IsStatic);

                    var hasThis = !method.IsStatic;

                    Debug.Assert(implementation.Parameters.Count == method.Parameters.Count + (hasThis ? 1 : 0));

                    if (implementation.HasExtraData<TranslatedMethodBody>())
                        continue; // Already has a translated body, skip.

                    Debug.Assert(!implementation.HasExtraData<NativeMethodBody>());

                    var argumentCount = method.Parameters.Count;

                    List<Instruction> instructions = new();

                    // Method info
                    {
                        var methodInfoField = method.MethodInfoField;
                        Debug.Assert(methodInfoField is not null);
                        Debug.Assert(implementation.DeclaringType is not null);

                        IReadOnlyList<TypeAnalysisContext> methodInfoGenericArguments = [.. implementation.DeclaringType.GenericParameters, .. implementation.GenericParameters];

                        instructions.Add(OpCodes.Ldsfld, methodInfoField.MaybeMakeConcreteGeneric(methodInfoGenericArguments));
                    }

                    // Object pointer
                    if (hasThis)
                    {
                        var parameter = implementation.Parameters[0];
                        var dataType = ((GenericInstanceTypeAnalysisContext)parameter.ParameterType).GenericArguments[0];

                        instructions.Add(OpCodes.Ldarg, parameter);
                        instructions.Add(OpCodes.Call, getPointerForThis.MakeGenericInstanceMethod(dataType));
                    }
                    else
                    {
                        instructions.Add(OpCodes.Ldc_I4_0);
                        instructions.Add(OpCodes.Conv_I);
                    }

                    // Arguments array
                    if (argumentCount > 0)
                    {
                        instructions.Add(OpCodes.Ldc_I4, argumentCount);
                        instructions.Add(OpCodes.Conv_U);
                        instructions.Add(OpCodes.Sizeof, appContext.SystemTypes.SystemIntPtrType);
                        instructions.Add(OpCodes.Mul_Ovf_Un);
                        instructions.Add(OpCodes.Localloc);

                        var startIndex = hasThis ? 1 : 0;
                        for (var i = 0; i < argumentCount; i++)
                        {
                            var parameter = implementation.Parameters[startIndex + i];
                            var dataType = ((GenericInstanceTypeAnalysisContext)parameter.ParameterType).GenericArguments[0];
                            instructions.Add(OpCodes.Dup);
                            AddOffsetForPointerIndex(instructions, i, appContext);
                            instructions.Add(OpCodes.Ldarg, parameter);
                            instructions.Add(OpCodes.Call, getPointerForParameter.MakeGenericInstanceMethod(dataType));
                            instructions.Add(OpCodes.Stind_I);
                        }
                    }
                    else
                    {
                        instructions.Add(OpCodes.Ldc_I4_0);
                        instructions.Add(OpCodes.Conv_U);
                    }

                    if (implementation.IsVoid)
                    {
                        instructions.Add(OpCodes.Call, invokeAction);
                    }
                    else
                    {
                        instructions.Add(OpCodes.Call, invokeFunction.MakeGenericInstanceMethod(implementation.ReturnType));
                    }

                    instructions.Add(OpCodes.Ret);

                    implementation.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = instructions,
                    });
                }
            }
        }
    }

    private static void AddOffsetForPointerIndex(List<Instruction> instructions, int index, ApplicationAnalysisContext appContext)
    {
        if (index != 0)
        {
            if (index > 1)
            {
                instructions.Add(OpCodes.Ldc_I4, index);
                instructions.Add(OpCodes.Conv_I);
            }

            instructions.Add(OpCodes.Sizeof, appContext.SystemTypes.SystemIntPtrType);

            if (index > 1)
            {
                instructions.Add(OpCodes.Mul);
            }

            instructions.Add(OpCodes.Add);
        }
    }
}
