using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class ObjectInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Object Interface Processor";
    public override string Id => "object_interface_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var il2CppMscorlib = appContext.Il2CppMscorlib;

        var il2CppSystemObject = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemValueType = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");
        var il2CppSystemEnum = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");

        var il2CppSystemIObject = InjectInterface(appContext, "IObject");
        var il2CppSystemIValueType = InjectInterface(appContext, "IValueType");
        var il2CppSystemIEnum = InjectInterface(appContext, "IEnum");

        il2CppSystemIEnum.InterfaceContexts.Add(il2CppSystemIValueType);

        il2CppSystemValueType.InterfaceContexts.Add(il2CppSystemIValueType);
        il2CppSystemEnum.InterfaceContexts.Add(il2CppSystemIEnum);

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                if (type != il2CppSystemIObject)
                {
                    type.InterfaceContexts.Add(il2CppSystemIObject);
                }

                if (type.DefaultBaseType == il2CppSystemValueType)
                {
                    type.InterfaceContexts.Add(il2CppSystemIValueType);
                }
                else if (type.DefaultBaseType == il2CppSystemEnum)
                {
                    type.InterfaceContexts.Add(il2CppSystemIValueType);
                    type.InterfaceContexts.Add(il2CppSystemIEnum);
                }
            }
        }
    }

    private static InjectedTypeAnalysisContext InjectInterface(ApplicationAnalysisContext appContext, string name)
    {
        var result = appContext.Il2CppMscorlib.InjectType("Il2CppSystem", name, appContext.SystemTypes.SystemObjectType,
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        result.IsInjected = true;
        return result;
    }
}
