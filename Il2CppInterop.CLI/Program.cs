using Cpp2IL.Core.ProcessingLayers;
using Il2CppInterop.Generator;

string gameExePath = args[0];
string outputFolder = args[1];
string unstripDirectory = args[2];
Il2CppGame.Process(
    gameExePath,
    outputFolder,
    new AsmResolverDllOutputFormatBinding(),
    [
        new AttributeAnalysisProcessingLayer(), // Needed for recovery of params and unmanaged constraints
        new StableRenamingProcessingLayer(),
        new UnstripProcessingLayer(), // Can be disabled for performance during development
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
        new MarshallingProcessingLayer(),
        new PrimitiveImplicitConversionProcessingLayer(),
        new EnumProcessingLayer(),
        new FieldAccessorProcessingLayer(),
        new ExceptionHierarchyProcessingLayer(),
        new MethodBodyTranslationProcessingLayer(),
        new NativeMethodBodyProcessingLayer(),
        new DelegateConversionProcessingLayer(),
        new ByRefParameterOverloadProcessingLayer(),
        new UserFriendlyOverloadProcessingLayer(),
        // new SystemInterfaceRecoveryProcessingLayer(), // Should handle INotifyCompletion, IEnumerable, IEquatable, etc
        new ConstantInitializationProcessingLayer(),
        new StaticConstructorProcessingLayer(),
    ],
    [new(UnstripBaseProcessingLayer.DirectoryKey, unstripDirectory)]);
Console.WriteLine("Done!");

/*
Todo:
- Object/ValueType/Enum special handling
- Creation method registration
- ICall runtime delegate creation
- System interfaces
- Multidimensional arrays https://github.com/js6pak/libil2cpp-archive/blob/90c6b7ed1c291d54b257d751a4d743d07dea8d62/vm/Array.cpp#L273-L286
- Unstripped class injection
- Finalizers might need renamed/removed
- Improve ByReference<T> and Pointer<T> semantics in unstripped methods
- overloads with arrays, delegates, primitives
*/
