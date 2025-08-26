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
        // Il2CppSystem.Object
        {
            var il2CppType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");

            // Need to fix a couple method attributes, so that they are not marked as newslot

            // bool Equals(object)
            {
                var equalsMethod = il2CppType.Methods.First(m => m.Name == nameof(object.Equals) && m.Parameters.Count == 1);
                equalsMethod.OverrideAttributes = equalsMethod.Attributes & ~MethodAttributes.NewSlot;
            }

            // int GetHashCode()
            {
                var getHashCodeMethod = il2CppType.Methods.First(m => m.Name == nameof(object.GetHashCode) && m.Parameters.Count == 0);
                getHashCodeMethod.OverrideAttributes = getHashCodeMethod.Attributes & ~MethodAttributes.NewSlot;
            }
        }

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
                }

                foreach (var field in type.Fields)
                {
                    if (field.IsInjected)
                    {
                        continue;
                    }

                    // Change constant fields to static readonly
                    if (field.Attributes.HasFlag(FieldAttributes.Literal))
                    {
                        field.OverrideAttributes = (field.Attributes & ~FieldAttributes.Literal) | FieldAttributes.InitOnly;
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
