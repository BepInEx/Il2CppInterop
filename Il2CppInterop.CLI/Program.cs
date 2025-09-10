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
        new ConflictRenamingProcessingLayer(),
        new AttributesOverrideProcessingLayer(),
        new PublicizerProcessingLayer(),
        new MscorlibAssemblyInjectionProcessingLayer(),
        new ReferenceAssemblyInjectionProcessingLayer(),
        new ObjectInterfaceProcessingLayer(),
        new ReferenceReplacementProcessingLayer(),
        new AttributeRemovalProcessingLayer(),
        new InitializationClassProcessingLayer(),
        new PointerConstructorProcessingLayer(),
        new MarshallingProcessingLayer(),
        new PrimitiveImplicitConversionProcessingLayer(),
        new EnumProcessingLayer(),
        new MemberAttributeProcessingLayer(),
        new FieldAccessorProcessingLayer(),
        new EventProcessingLayer(),
        new ExceptionHierarchyProcessingLayer(),
        new MethodInvokerProcessingLayer(),
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
- overloads with arrays, delegates, primitives

Problem:
- UnstripProcessingLayer creates lots of concrete generics before ReferenceReplacementProcessingLayer runs.
  This causes issues like generic instance methods returning Il2CppSystem.Void instead of System.Void.
  Presumeably, it could also cause other issues like using sz arrays instead of the Il2Cpp array types.
*/
