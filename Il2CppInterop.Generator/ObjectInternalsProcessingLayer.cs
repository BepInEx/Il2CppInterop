using System.Reflection;
using AsmResolver.DotNet.Code.Cil;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Generator;

public class ObjectInternalsProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Object Internals Processor";
    public override string Id => "object_internals_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");

        var il2CppStaticClass = appContext.ResolveTypeOrThrow(typeof(IL2CPP));

        var il2CppObjectPool = appContext.ResolveTypeOrThrow(typeof(Il2CppObjectPool));

        var pooledPointerField = new InjectedFieldAnalysisContext(
            "_pooledPointer",
            appContext.SystemTypes.SystemIntPtrType,
            FieldAttributes.Private,
            il2CppSystemObject)
        {
            IsInjected = true,
        };
        il2CppSystemObject.Fields.Add(pooledPointerField);

        var gcHandleField = new InjectedFieldAnalysisContext(
            "_gcHandle",
            appContext.SystemTypes.SystemIntPtrType,
            FieldAttributes.Private,
            il2CppSystemObject)
        {
            IsInjected = true,
        };
        il2CppSystemObject.Fields.Add(gcHandleField);

        // Constructor
        {
            var constructor = il2CppSystemObject.PointerConstructor!;
            var baseConstructor = appContext.SystemTypes.SystemObjectType.Methods.Single(m => m.IsInstanceConstructor);
            var objectPointerType = constructor.Parameters[0].ParameterType;
            var objectPointerToSystemIntPtr = objectPointerType.GetExplicitConversionTo(appContext.SystemTypes.SystemIntPtrType);

            constructor.PutExtraData(new NativeMethodBody()
            {
                Instructions = [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Call, baseConstructor),

                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Ldarg_1),
                    new Instruction(CilOpCodes.Call, objectPointerToSystemIntPtr),
                    new Instruction(CilOpCodes.Stfld, pooledPointerField),

                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Ldarg_1),
                    new Instruction(CilOpCodes.Call, objectPointerToSystemIntPtr),
                    new Instruction(CilOpCodes.Ldc_I4_0), // false for "pinned"
                    new Instruction(CilOpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.il2cpp_gchandle_new))),
                    new Instruction(CilOpCodes.Stfld, gcHandleField),

                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        // Pointer property
        {
            var getMethod = new InjectedMethodAnalysisContext(
                il2CppSystemObject,
                $"get_{nameof(Object.Pointer)}",
                appContext.SystemTypes.SystemIntPtrType,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                [])
            {
                IsInjected = true,
            };
            il2CppSystemObject.Methods.Add(getMethod);

            var property = new InjectedPropertyAnalysisContext(
                nameof(Object.Pointer),
                appContext.SystemTypes.SystemIntPtrType,
                getMethod,
                null,
                PropertyAttributes.None,
                il2CppSystemObject)
            {
                IsInjected = true,
            };
            il2CppSystemObject.Properties.Add(property);

            getMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Ldfld, gcHandleField),
                    new Instruction(CilOpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.Il2CppGCHandleGetTargetOrThrow))),
                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        // WasCollected property
        {
            var getMethod = new InjectedMethodAnalysisContext(
                il2CppSystemObject,
                $"get_{nameof(Object.WasCollected)}",
                appContext.SystemTypes.SystemBooleanType,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                [])
            {
                IsInjected = true,
            };

            il2CppSystemObject.Methods.Add(getMethod);
            var property = new InjectedPropertyAnalysisContext(
                nameof(Object.WasCollected),
                appContext.SystemTypes.SystemBooleanType,
                getMethod,
                null,
                PropertyAttributes.None,
                il2CppSystemObject)
            {
                IsInjected = true,
            };
            il2CppSystemObject.Properties.Add(property);

            getMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Ldfld, gcHandleField),
                    new Instruction(CilOpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.Il2CppGCHandleGetTargetWasCollected))),
                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        // Finalizer
        {
            var baseMethod = appContext.SystemTypes.SystemObjectType.GetMethodByName("Finalize");
            var method = new InjectedMethodAnalysisContext(
                il2CppSystemObject,
                "Finalize",
                appContext.SystemTypes.SystemVoidType,
                MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                [])
            {
                IsInjected = true,
            };
            il2CppSystemObject.Methods.Add(method);

            method.OverridesList.Add(baseMethod); // Currently doesn't work because Cpp2IL excludes overrides from non-interface methods

            var instructions = new List<Instruction>();

            var returnInstruction = new Instruction(CilOpCodes.Ret);

            var tryStart = instructions.Add(CilOpCodes.Ldarg_0);
            instructions.Add(CilOpCodes.Ldfld, gcHandleField);
            instructions.Add(CilOpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.il2cpp_gchandle_free)));

            instructions.Add(CilOpCodes.Ldarg_0);
            instructions.Add(CilOpCodes.Ldfld, pooledPointerField);
            instructions.Add(CilOpCodes.Call, il2CppObjectPool.GetMethodByName(nameof(Il2CppObjectPool.Remove)));
            instructions.Add(CilOpCodes.Leave, returnInstruction);

            var handlerStart = instructions.Add(CilOpCodes.Ldarg_0);
            instructions.Add(CilOpCodes.Call, baseMethod);
            instructions.Add(CilOpCodes.Endfinally);

            instructions.Add(returnInstruction);

            var exceptionHandler = new ExceptionHandler()
            {
                HandlerType = CilExceptionHandlerType.Finally,
                TryStart = tryStart,
                TryEnd = handlerStart,
                HandlerStart = handlerStart,
                HandlerEnd = returnInstruction,
            };

            method.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions,
                ExceptionHandlers = [exceptionHandler],
            });
        }
    }
}
