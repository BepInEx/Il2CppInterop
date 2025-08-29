using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class FieldAccessorProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Field Accessor Processor";
    public override string Id => "field_accessor_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var fieldAccessHelper = appContext.ResolveTypeOrThrow(typeof(FieldAccessHelper));
        var getStaticFieldValue = fieldAccessHelper.GetMethodByName(nameof(FieldAccessHelper.GetStaticFieldValue));
        var setStaticFieldValue = fieldAccessHelper.GetMethodByName(nameof(FieldAccessHelper.SetStaticFieldValue));
        var getInstanceFieldValue = fieldAccessHelper.GetMethodByName(nameof(FieldAccessHelper.GetInstanceFieldValue));
        var setInstanceFieldValue_Wbarrior = fieldAccessHelper.GetMethodByName(nameof(FieldAccessHelper.SetInstanceFieldValue_Wbarrior));
        var setInstanceFieldValue_Pointer = fieldAccessHelper.GetMethodByName(nameof(FieldAccessHelper.SetInstanceFieldValue_Pointer));

        var setInstanceFieldValue = appContext.Binary.GetExportedFunctions().Any(pair => pair.Key == "il2cpp_gc_wbarrier_set_field")
            ? setInstanceFieldValue_Wbarrior
            : setInstanceFieldValue_Pointer;

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                var fields = type.IsValueType
                    ? type.Fields.Where(f => f.IsStatic).ToArray()
                    : type.Fields.ToArray();

                foreach (var field in fields)
                {
                    if (field.ConstantValue is not null)
                    {
                        Debug.Assert(field.IsStatic);
                        continue; // Skip fields with constant values, as they are not suitable for property conversion.
                    }

                    if (field.IsInjected)
                        continue;

                    var methodAttributes = field.IsStatic
                        ? MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static
                        : MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

                    var getMethod = new InjectedMethodAnalysisContext(
                        field.DeclaringType,
                        "get_" + field.Name,
                        field.FieldType,
                        methodAttributes,
                        [])
                    {
                        IsInjected = true,
                    };
                    field.DeclaringType.Methods.Add(getMethod);

                    // get accessor body
                    {
                        var instructions = new List<Instruction>();
                        if (field.IsStatic)
                        {
                            instructions.Add(OpCodes.Ldsfld, field.GetInstantiatedFieldInfoAddressStorage());
                            instructions.Add(OpCodes.Call, getStaticFieldValue.MakeGenericInstanceMethod([field.FieldType]));
                            instructions.Add(OpCodes.Ret);
                        }
                        else
                        {
                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Ldsfld, field.GetInstantiatedOffsetStorage());
                            instructions.Add(OpCodes.Call, getInstanceFieldValue.MakeGenericInstanceMethod([field.FieldType]));
                            instructions.Add(OpCodes.Ret);
                        }

                        getMethod.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                        });
                    }

                    var setMethod = new InjectedMethodAnalysisContext(
                        field.DeclaringType,
                        "set_" + field.Name,
                        field.AppContext.SystemTypes.SystemVoidType,
                        methodAttributes,
                        [field.FieldType])
                    {
                        IsInjected = true,
                    };
                    field.DeclaringType.Methods.Add(setMethod);

                    // set accessor body
                    {
                        var instructions = new List<Instruction>();
                        if (field.IsStatic)
                        {
                            instructions.Add(OpCodes.Ldsfld, field.GetInstantiatedFieldInfoAddressStorage());
                            instructions.Add(OpCodes.Ldarg, setMethod.Parameters[0]);
                            instructions.Add(OpCodes.Call, setStaticFieldValue.MakeGenericInstanceMethod([field.FieldType]));
                            instructions.Add(OpCodes.Ret);
                        }
                        else
                        {
                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Ldsfld, field.GetInstantiatedOffsetStorage());
                            instructions.Add(OpCodes.Ldarg, setMethod.Parameters[0]);
                            instructions.Add(OpCodes.Call, setInstanceFieldValue.MakeGenericInstanceMethod([field.FieldType]));
                            instructions.Add(OpCodes.Ret);
                        }

                        setMethod.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                        });
                    }

                    var property = new InjectedPropertyAnalysisContext(
                        field.Name,
                        field.FieldType,
                        getMethod,
                        setMethod,
                        PropertyAttributes.None,
                        field.DeclaringType)
                    {
                        IsInjected = true,
                    };
                    field.DeclaringType.Properties.Add(property);

                    property.OriginalField = field;
                    field.PropertyAccessor = property;

                    field.DeclaringType.Fields.Remove(field);
                }
            }
        }
    }
}
