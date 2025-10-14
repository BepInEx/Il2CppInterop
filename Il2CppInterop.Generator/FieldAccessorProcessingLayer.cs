using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime.Attributes;
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

        var il2CppFieldAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppFieldAttribute));
        var il2CppFieldAttributeConstructor = il2CppFieldAttribute.GetMethodByName(".ctor");

        var il2CppMemberAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppMemberAttribute));
        var il2CppMemberAttributeName = il2CppMemberAttribute.GetPropertyByName(nameof(Il2CppMemberAttribute.Name));

        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");
        var byReferenceStatic = appContext.ResolveTypeOrThrow(typeof(ByReference));
        var byReferenceStatic_GetReferenceAtOffset = byReferenceStatic.GetMethodByName(nameof(ByReference.GetReferenceAtOffset));

        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemObject_get_Pointer = il2CppSystemObject.GetMethodByName($"get_{nameof(Object.Pointer)}");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                var instantiatedType = type.SelfInstantiateIfGeneric();
                var isValueType = type.IsValueType;
                HashSet<string> existingNames = [type.CSharpName, .. type.Members.Select(m => m.Name)];

                for (var i = type.Fields.Count - 1; i >= 0; i--)
                {
                    var field = type.Fields[i];
                    if (field.ConstantValue is not null)
                    {
                        Debug.Assert(field.IsStatic);
                        continue; // Skip fields with constant values, as they are not suitable for property conversion.
                    }

                    if (field.IsInjected)
                        continue;

                    if (!field.IsStatic && !type.IsIl2CppPrimitive)
                    {
                        var name = GetNonConflictingName($"GetFieldAddress_{field.Name}", existingNames);
                        var parameterType = isValueType ? byReference.MakeGenericInstanceType([instantiatedType]) : instantiatedType;
                        var getFieldAddress = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            byReference.MakeGenericInstanceType([field.FieldType]),
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [parameterType])
                            {
                                IsInjected = true,
                            };
                        type.Methods.Add(getFieldAddress);

                        if (isValueType)
                        {
                            getFieldAddress.PutExtraData(new NativeMethodBody()
                            {
                                Instructions = [
                                    new Instruction(OpCodes.Ldarg, getFieldAddress.Parameters[0]),
                                    new Instruction(OpCodes.Ldsfld, field.GetInstantiatedOffsetStorage()),
                                    new Instruction(OpCodes.Call, byReferenceStatic_GetReferenceAtOffset.MakeGenericInstanceMethod(instantiatedType, field.FieldType)),
                                    new Instruction(OpCodes.Ret)
                                ],
                            });
                        }
                        else
                        {
                            getFieldAddress.PutExtraData(new NativeMethodBody()
                            {
                                Instructions = [
                                    new Instruction(OpCodes.Ldarg, getFieldAddress.Parameters[0]),
                                    new Instruction(OpCodes.Callvirt, il2CppSystemObject_get_Pointer),
                                    new Instruction(OpCodes.Ldsfld, field.GetInstantiatedOffsetStorage()),
                                    new Instruction(OpCodes.Conv_I),
                                    new Instruction(OpCodes.Add),
                                    new Instruction(OpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([field.FieldType], [])),
                                    new Instruction(OpCodes.Ret)
                                ],
                            });
                        }

                        field.FieldAddressAccessor = getFieldAddress;
                    }

                    if (isValueType && !field.IsStatic)
                    {
                        // Instance fields in value types do not get converted into properties.

                        var attribute = new AnalyzedCustomAttribute(il2CppFieldAttributeConstructor);
                        if (field.Name != field.DefaultName)
                        {
                            var parameter = new CustomAttributePrimitiveParameter(field.DefaultName, attribute, CustomAttributeParameterKind.Property, 0);
                            attribute.Properties.Add(new CustomAttributeProperty(il2CppMemberAttributeName, parameter));
                        }
                        field.CustomAttributes ??= new(1);
                        field.CustomAttributes.Add(attribute);

                        continue;
                    }

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
                        OriginalField = field,
                    };
                    field.PropertyAccessor = property;
                    field.DeclaringType.Properties.Add(property);

                    // Il2CppFieldAttribute
                    {
                        var attribute = new AnalyzedCustomAttribute(il2CppFieldAttributeConstructor);
                        if (property.Name != field.DefaultName)
                        {
                            var parameter = new CustomAttributePrimitiveParameter(field.DefaultName, attribute, CustomAttributeParameterKind.Property, 0);
                            attribute.Properties.Add(new CustomAttributeProperty(il2CppMemberAttributeName, parameter));
                        }
                        property.CustomAttributes = [attribute];
                    }

                    field.DeclaringType.Fields.RemoveAt(i);
                }
            }
        }
    }

    private static string GetNonConflictingName(string baseName, HashSet<string> existingNames)
    {
        var name = baseName;
        while (existingNames.Contains(name))
        {
            name = $"{baseName}_";
        }
        return name;
    }
}
