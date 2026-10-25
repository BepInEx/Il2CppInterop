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

            assembly.Culture = null;
            assembly.Version = new(0, 0, 0, 0);
            assembly.PublicKey = null;
            assembly.PublicKeyToken = null;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                {
                    continue;
                }

                if (type.IsStatic)
                {
                    type.Attributes &= ~TypeAttributes.Sealed;
                }

                // Remove bad flags from type
                {
#pragma warning disable SYSLIB0050 // Type or member is obsolete
                    const TypeAttributes TypeFlagsToRemove =
                        TypeAttributes.AutoClass |
                        TypeAttributes.HasSecurity |
                        TypeAttributes.Import |
                        TypeAttributes.RTSpecialName |
                        TypeAttributes.Serializable |
                        TypeAttributes.UnicodeClass |
                        TypeAttributes.WindowsRuntime;
#pragma warning restore SYSLIB0050 // Type or member is obsolete

                    type.Attributes &= ~TypeFlagsToRemove;
                }

                if (type.IsValueType)
                {
                    const TypeAttributes TypeFlagsToRemove =
                        TypeAttributes.Abstract |
                        TypeAttributes.ClassSemanticsMask;
                    type.Attributes &= ~TypeFlagsToRemove;
                }
                else
                {
                    type.Attributes &= ~TypeAttributes.LayoutMask;
                }

                foreach (var genericParameter in type.GenericParameters)
                {
                    const GenericParameterAttributes GenericParamFlagsToRemove =
                        GenericParameterAttributes.Covariant |
                        GenericParameterAttributes.Contravariant;
                    genericParameter.Attributes &= ~GenericParamFlagsToRemove;
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

                    method.Attributes &= ~FlagsToRemove;

                    method.ImplAttributes = MethodImplAttributes.Managed;

                    foreach (var parameter in method.Parameters)
                    {
                        const ParameterAttributes ParamFlagsToRemove =
                            ParameterAttributes.Optional |
                            ParameterAttributes.HasDefault |
                            ParameterAttributes.HasFieldMarshal;
                        parameter.Attributes &= ~ParamFlagsToRemove;
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
                        field.Attributes = (field.Attributes & ~FieldAttributes.Literal) | FieldAttributes.InitOnly;
                    }
                    else
                    {
                        // Remove readonly from non-constant fields
                        field.Attributes &= ~FieldAttributes.InitOnly;
                    }

                    const FieldAttributes FlagsToRemove =
                        FieldAttributes.HasFieldRVA |
                        FieldAttributes.HasDefault |
                        FieldAttributes.HasFieldMarshal;

                    field.Attributes &= ~FlagsToRemove;
                }

                foreach (var property in type.Properties)
                {
                    if (property.IsInjected)
                    {
                        continue;
                    }

                    const PropertyAttributes FlagsToRemove =
                        PropertyAttributes.HasDefault;

                    property.Attributes &= ~FlagsToRemove;
                }

                // There are no event attributes that need to be modified.
            }
        }
    }
}
