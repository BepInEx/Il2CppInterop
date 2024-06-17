using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Passes;

internal class Pass61ImplementAwaiters
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var corlib = context.GetAssemblyByName("mscorlib");
        var actionUntyped = corlib.GetTypeByName("System.Action");

        var actionConversionUntyped = actionUntyped.NewType.Methods.FirstOrDefault(m => m.Name == "op_Implicit") ?? throw new MissingMethodException("Untyped action conversion");

        foreach (var assemblyContext in context.Assemblies)
        {
            // dont actually import the references until they're needed
            Lazy<TypeReference> actionUntypedRef = new(() => assemblyContext.NewAssembly.MainModule.ImportReference(actionUntyped.OriginalType));
            Lazy<MethodReference> actionConversionUntypedRef = new(() => assemblyContext.NewAssembly.MainModule.ImportReference(actionConversionUntyped));
            Lazy<TypeReference> notifyCompletionRef = new(() => assemblyContext.NewAssembly.MainModule.ImportReference(typeof(INotifyCompletion)));
            var voidRef = assemblyContext.Imports.Module.Void();
            foreach (var typeContext in assemblyContext.Types)
            {
                var interfaceImplementation = typeContext.OriginalType.Interfaces.FirstOrDefault(InterfaceImplementation => InterfaceImplementation.InterfaceType.Name == nameof(INotifyCompletion));
                if (interfaceImplementation is null)
                    continue;

                var isGeneric = typeContext.OriginalType.ContainsGenericParameter;

                var awaiterType = typeContext.OriginalType;

                var originalOnComplete = typeContext.TryGetMethodByName(nameof(INotifyCompletion.OnCompleted)) ?? throw new MissingMethodException("Original OnComplete");

                var onCompletedAttr = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;
                var onComplete = new MethodDefinition(nameof(INotifyCompletion.OnCompleted), onCompletedAttr, voidRef);
                typeContext.NewType.Interfaces.Add(new(notifyCompletionRef.Value));
                typeContext.NewType.Methods.Add(onComplete);

                onComplete.Parameters.Add(new ParameterDefinition("continuation", ParameterAttributes.None, actionUntypedRef.Value));

                var onCompleteIl = onComplete.Body.GetILProcessor();

                onCompleteIl.Emit(OpCodes.Nop);
                onCompleteIl.Emit(OpCodes.Ldarg_0);
                onCompleteIl.Emit(OpCodes.Ldarg_1); // ldarg1 bc not static, so ldarg0 is this & ldarg1 is the parameter
                onCompleteIl.Emit(OpCodes.Call, actionConversionUntypedRef.Value);
                onCompleteIl.Emit(OpCodes.Call, originalOnComplete.NewMethod);
                onCompleteIl.Emit(OpCodes.Nop);
                onCompleteIl.Emit(OpCodes.Ret);
            }
        }
    }
}
