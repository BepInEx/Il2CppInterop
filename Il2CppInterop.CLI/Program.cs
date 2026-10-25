using Cpp2IL.Core.Logging;
using Il2CppInterop.Generator;

string gameExePath = args[0];
string outputFolder = args[1];
string unstripDirectory = args[2];

// Unstrip directory needs to contain all files recursively contained in these directories:
// \Editor\Data\MonoBleedingEdge\lib\mono\unityaot-win32
// \Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_player_nondevelopment_il2cpp\Data\Managed
// For other platforms, the paths will be slightly different.

Logger.InfoLog += Console.WriteLine;
Logger.WarningLog += Console.WriteLine;
Logger.ErrorLog += Console.WriteLine;
Logger.VerboseLog += Console.WriteLine;

Il2CppGame.Process(
    gameExePath,
    outputFolder,
    new AsmResolverDllOutputFormatBinding(),
    Il2CppGame.GetDefaultProcessingLayers(),
    [new(UnstripBaseProcessingLayer.DirectoryKey, unstripDirectory)]);
Console.WriteLine("Done!");

/*
Todo
- Add attributes to "Unsafe" methods so that users cannot see them
- Lazily create field offset accessors
- Reduce number of implementation methods
- Make generic inflation more robust
*/
