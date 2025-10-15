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
                    new Instruction(OpCodes.Ldarg_0),
                    new Instruction(OpCodes.Call, baseConstructor),

                    new Instruction(OpCodes.Ldarg_0),
                    new Instruction(OpCodes.Ldarg_1),
                    new Instruction(OpCodes.Call, objectPointerToSystemIntPtr),
                    new Instruction(OpCodes.Stfld, pooledPointerField),

                    new Instruction(OpCodes.Ldarg_0),
                    new Instruction(OpCodes.Ldarg_1),
                    new Instruction(OpCodes.Call, objectPointerToSystemIntPtr),
                    new Instruction(OpCodes.Ldc_I4_0), // false for "pinned"
                    new Instruction(OpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.il2cpp_gchandle_new))),
                    new Instruction(OpCodes.Stfld, gcHandleField),

                    new Instruction(OpCodes.Ret),
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
                    new Instruction(OpCodes.Ldarg_0),
                    new Instruction(OpCodes.Ldfld, gcHandleField),
                    new Instruction(OpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.Il2CppGCHandleGetTargetOrThrow))),
                    new Instruction(OpCodes.Ret),
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
                    new Instruction(OpCodes.Ldarg_0),
                    new Instruction(OpCodes.Ldfld, gcHandleField),
                    new Instruction(OpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.Il2CppGCHandleGetTargetWasCollected))),
                    new Instruction(OpCodes.Ret),
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

            var instructions = new List<Instruction>();

            var returnInstruction = new Instruction(OpCodes.Ret);

            var tryStart = new Instruction(OpCodes.Nop);
            instructions.Add(tryStart);

            instructions.Add(OpCodes.Ldarg_0);
            instructions.Add(OpCodes.Ldfld, gcHandleField);
            instructions.Add(OpCodes.Call, il2CppStaticClass.GetMethodByName(nameof(IL2CPP.il2cpp_gchandle_free)));

            instructions.Add(OpCodes.Ldarg_0);
            instructions.Add(OpCodes.Ldfld, pooledPointerField);
            instructions.Add(OpCodes.Call, il2CppObjectPool.GetMethodByName(nameof(Il2CppObjectPool.Remove)));

            var tryEnd = new Instruction(OpCodes.Leave, returnInstruction);
            instructions.Add(tryEnd);

            var handlerStart = new Instruction(OpCodes.Nop);
            instructions.Add(handlerStart);

            instructions.Add(OpCodes.Ldarg_0);
            instructions.Add(OpCodes.Call, baseMethod);

            var handlerEnd = new Instruction(OpCodes.Endfinally);
            instructions.Add(handlerEnd);

            instructions.Add(returnInstruction);

            var exceptionHandler = new ExceptionHandler()
            {
                HandlerType = CilExceptionHandlerType.Finally,
                TryStart = tryStart,
                TryEnd = tryEnd,
                HandlerStart = handlerStart,
                HandlerEnd = handlerEnd,
            };

            method.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions,
                ExceptionHandlers = [exceptionHandler],
            });
        }
    }
}
