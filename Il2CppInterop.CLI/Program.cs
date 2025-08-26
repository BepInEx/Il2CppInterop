using Cpp2IL.Core.ProcessingLayers;
using Il2CppInterop.Generator;

string gameExePath = args[0];
string outputFolder = args[1];
Il2CppGame.Process(
    gameExePath,
    outputFolder,
    new AsmResolverDllOutputFormatBinding(),
    [
        new AttributeAnalysisProcessingLayer(), // Needed for recovery of params and unmanaged constraints
        new StableRenamingProcessingLayer(),
        new UnstripProcessingLayer(), // Can be disabled for performance during development
        new TypeInfoProcessingLayer(),
        new Il2CppRenamingProcessingLayer(),
        new CleanRenamingProcessingLayer(),
        new AttributesOverrideProcessingLayer(),
        new PublicizerProcessingLayer(),
        new MscorlibAssemblyInjectionProcessingLayer(),
        new ReferenceAssemblyInjectionProcessingLayer(),
        new ObjectInterfaceProcessingLayer(),
        new ReferenceReplacementProcessingLayer(),
        new AttributeRemovalProcessingLayer(),
        // Call analysis goes here or anywhere after this
        new InitializationClassProcessingLayer(),
        new PointerConstructorProcessingLayer(),
        new PrimitiveImplicitConversionProcessingLayer(),
        new EnumProcessingLayer(),
        new FieldAccessorProcessingLayer(),
        new ExceptionHierarchyProcessingLayer(),
        new MethodBodyTranslationProcessingLayer(),
        new NativeMethodBodyProcessingLayer(),
        new DelegateConversionProcessingLayer(),
        // new SystemInterfaceRecoveryProcessingLayer(), // Should handle INotifyCompletion, IEnumerable, IEquatable, etc
        new ConstantInitializationProcessingLayer(),
        new StaticConstructorProcessingLayer(),
    ]);
Console.WriteLine("Done!");

/*
Todo:
- Interface implementation for marshalling
- Il2Cppmscorlib cyclical dependency resolution - solved?
- Object/ValueType/Enum special handling
- Creation method registration
- ICall
- System interfaces
- Params (or collections expression support for arrays)
- Multidimensional arrays https://github.com/js6pak/libil2cpp-archive/blob/90c6b7ed1c291d54b257d751a4d743d07dea8d62/vm/Array.cpp#L273-L286
- Unstripped class injection
- Finalizers might need renamed/removed
*/
