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
        //new StableRenamingProcessingLayer(),
        new UnstripProcessingLayer(), // Can be disabled for performance during development
        new InterfaceOverrideProcessingLayer(),
        new InvalidFieldRemovalProcessingLayer(),
        new Il2CppRenamingProcessingLayer(),
        new CleanRenamingProcessingLayer(),
        new ConflictRenamingProcessingLayer(),
        new AttributesOverrideProcessingLayer(),
        new PublicizerProcessingLayer(),
        new MscorlibAssemblyInjectionProcessingLayer(),
        new KnownTypeAssignmentProcessingLayer(),
        new ReferenceAssemblyInjectionProcessingLayer(),
        new InvisibleInterfaceProcessingLayer(),
        new ObjectInterfaceProcessingLayer(),
        new ReferenceReplacementProcessingLayer(),
        new AttributeRemovalProcessingLayer(),
        new IndexerAttributeInjectionProcessingLayer(),
        new PointerConstructorProcessingLayer(),
        new Il2CppTypeConstraintProcessingLayer(),
        new InitializationClassProcessingLayer(),
        new MarshallingProcessingLayer(),
        new PrimitiveImplicitConversionProcessingLayer(),
        new EnumProcessingLayer(),
        new ObjectOverridesProcessingLayer(),
        new ObjectInternalsProcessingLayer(),
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
Todo

Required for runtime testing:
- Class injection rewrite
- HarmonySupport rewrite
- ICall runtime delegate creation
- Bump to .NET 10

Ideal:
- Fix abstract methods having helper methods
- System interfaces
- overloads with arrays, delegates, primitives
- Source generation for user-injected types
- Add attributes to "Unsafe" methods so that users cannot see them
- Use stack analysis to improve unstripping
*/
