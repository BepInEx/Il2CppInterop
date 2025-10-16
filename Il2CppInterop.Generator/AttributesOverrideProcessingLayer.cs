using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class AttributesOverrideProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "attributes_override";
    public override string Name => "Attributes Override";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
            {
                continue;
            }

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                {
                    continue;
                }

                if (type.IsStatic)
                {
                    type.OverrideAttributes = type.Attributes & ~TypeAttributes.Sealed;

                    // We add a private constructor to prevent instantiation.
                    var constructor = new InjectedMethodAnalysisContext(
                        type,
                        ".ctor",
                        appContext.SystemTypes.SystemVoidType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                        [])
                    {
                        IsInjected = true
                    };
                    type.Methods.Add(constructor);
                }

                foreach (var method in type.Methods)
                {
                    if (method.IsInjected)
                    {
                        continue;
                    }

                    const MethodAttributes FlagsToRemove =
                        MethodAttributes.HasSecurity |
                        MethodAttributes.RequireSecObject |
                        MethodAttributes.UnmanagedExport |
                        MethodAttributes.PinvokeImpl;

                    method.OverrideAttributes = method.Attributes & ~FlagsToRemove;

                    method.OverrideImplAttributes = MethodImplAttributes.Managed;

                    foreach (var parameter in method.Parameters)
                    {
                        // Todo: make a Cpp2IL pull request so that Cpp2IL respects HasDefault.
                        // https://github.com/SamboyCoding/Cpp2IL/blob/d5260685fddb380f0ee884521d28c8309b2aff7e/Cpp2IL.Core/Model/Contexts/ParameterAnalysisContext.cs#L57
                        // https://github.com/SamboyCoding/Cpp2IL/blob/d5260685fddb380f0ee884521d28c8309b2aff7e/Cpp2IL.Core/Model/Contexts/ParameterAnalysisContext.cs#L75-L78
                        // https://github.com/SamboyCoding/Cpp2IL/blob/d5260685fddb380f0ee884521d28c8309b2aff7e/Cpp2IL.Core/Utils/AsmResolver/AsmResolverAssemblyPopulator.cs#L377-L386
                        const ParameterAttributes ParamFlagsToRemove =
                            ParameterAttributes.Optional |
                            ParameterAttributes.HasDefault |
                            ParameterAttributes.HasFieldMarshal;
                        parameter.OverrideAttributes = parameter.Attributes & ~ParamFlagsToRemove;
                    }
                }

                foreach (var field in type.Fields)
                {
                    if (field.IsInjected)
                    {
                        continue;
                    }

                    if (field.Attributes.HasFlag(FieldAttributes.Literal))
                    {
                        // Change constant fields to static readonly
                        field.OverrideAttributes = (field.Attributes & ~FieldAttributes.Literal) | FieldAttributes.InitOnly;
                    }
                    else
                    {
                        // Remove readonly from non-constant fields
                        field.OverrideAttributes = field.Attributes & ~FieldAttributes.InitOnly;
                    }

                    const FieldAttributes FlagsToRemove =
                        FieldAttributes.HasFieldRVA |
                        FieldAttributes.HasDefault |
                        FieldAttributes.HasFieldMarshal;

                    field.OverrideAttributes = field.Attributes & ~FlagsToRemove;
                }
            }
        }
    }
}
