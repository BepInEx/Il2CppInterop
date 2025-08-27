using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL;

namespace Il2CppInterop.Generator;

public static class Il2CppGame
{
    static Il2CppGame()
    {
        Logger.InfoLog += Console.WriteLine;
        Logger.WarningLog += Console.WriteLine;
        Logger.ErrorLog += Console.WriteLine;
        Logger.VerboseLog += Console.WriteLine;

        InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_32);
        InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_64);
        InstructionSetRegistry.RegisterInstructionSet<WasmInstructionSet>(DefaultInstructionSets.WASM);
        InstructionSetRegistry.RegisterInstructionSet<ArmV7InstructionSet>(DefaultInstructionSets.ARM_V7);
        InstructionSetRegistry.RegisterInstructionSet<NewArmV8InstructionSet>(DefaultInstructionSets.ARM_V8);

        LibCpp2IlBinaryRegistry.RegisterBuiltInBinarySupport();
    }

    public static void Process(string gameExePath, string outputFolder, Cpp2IlOutputFormat outputFormat, List<Cpp2IlProcessingLayer> processingLayers, KeyValuePair<string, string>[] extraData)
    {
        Process(gameExePath, processingLayers, extraData);

        outputFormat.DoOutput(GetCurrentAppContext(), outputFolder);
    }

    public static void Process(string gameExePath, List<Cpp2IlProcessingLayer> processingLayers, KeyValuePair<string, string>[] extraData)
    {
        var gameExeName = Path.GetFileNameWithoutExtension(gameExePath);

        var gameDirectory = Path.GetDirectoryName(gameExePath)!;

        var GameDataPath = Path.Combine(gameDirectory, $"{gameExeName}_Data");

        var UnityPlayerPath = Path.Combine(gameDirectory, "UnityPlayer.dll");

        var GameAssemblyPath = Path.Combine(gameDirectory, "GameAssembly.dll");

        var MetaDataPath = Path.Combine(GameDataPath, "il2cpp_data", "Metadata", "global-metadata.dat");

        var UnityVersion = Cpp2IlApi.DetermineUnityVersion(UnityPlayerPath, GameDataPath);

        Cpp2IlApi.InitializeLibCpp2Il(GameAssemblyPath, MetaDataPath, UnityVersion, false);

        foreach ((var key, var value) in extraData)
        {
            Cpp2IlApi.CurrentAppContext.PutExtraData(key, value);
        }

        foreach (var cpp2IlProcessingLayer in processingLayers)
        {
            cpp2IlProcessingLayer.PreProcess(GetCurrentAppContext(), processingLayers);
        }

        foreach (var cpp2IlProcessingLayer in processingLayers)
        {
            cpp2IlProcessingLayer.Process(GetCurrentAppContext());
        }
    }

    private static ApplicationAnalysisContext GetCurrentAppContext()
    {
        return Cpp2IlApi.CurrentAppContext ?? throw new NullReferenceException();
    }
}
