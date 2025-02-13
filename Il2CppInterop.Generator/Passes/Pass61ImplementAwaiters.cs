using System.Runtime.CompilerServices;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass61ImplementAwaiters
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var corlib = context.CorLib;

        var actionUntyped = corlib.GetTypeByName("System.Action");

        var actionConversion = actionUntyped.NewType.Methods.Single(m => m.Name == "op_Implicit");

        foreach (var assemblyContext in context.Assemblies)
        {
            // Use Lazy as a lazy way to not actually import the references until they're needed

            Lazy<ITypeDefOrRef> actionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(actionConversion.Parameters[0].ParameterType.ToTypeDefOrRef())!);
            Lazy<IMethodDefOrRef> actionConversionRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportMethod(actionConversion));
            Lazy<ITypeDefOrRef> notifyCompletionRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(typeof(INotifyCompletion)));
            var voidRef = assemblyContext.NewAssembly.ManifestModule!.CorLibTypeFactory.Void;

            foreach (var typeContext in assemblyContext.Types)
            {
                // Odds are a majority of types won't implement any interfaces. Skip them to save time.
                if (typeContext.OriginalType.IsInterface || typeContext.OriginalType.Interfaces.Count == 0)
                    continue;

                var iNotifyCompletion = typeof(INotifyCompletion);
                var interfaceImplementation = typeContext.OriginalType.Interfaces.SingleOrDefault(interfaceImpl => interfaceImpl.Interface?.Namespace == iNotifyCompletion.Namespace && interfaceImpl.Interface?.Name == iNotifyCompletion.Name);
                if (interfaceImplementation is null)
                    continue;

                var allOnCompleted = typeContext.Methods.Where(m => m.OriginalMethod.Name == nameof(INotifyCompletion.OnCompleted)).Select(mc => mc.NewMethod).ToArray();

                // Conversion spits out an Il2CppSystem.Action, so look for methods that take that (and only that) in & return void, so the stack is balanced
                // And use SignatureComparer because otherwise equality checks would fail due to the TypeSignatures being different references
                var interopOnCompleted = allOnCompleted.FirstOrDefault(m => !m.IsStatic && m.Parameters.Count == 1 && m.Signature is not null && SignatureComparer.Default.Equals(m.Signature.ReturnType, voidRef) && SignatureComparer.Default.Equals(m.Signature.ParameterTypes[0], actionConversion.Signature?.ReturnType));

                if (interopOnCompleted is null)
                {
                    var typeName = typeContext.OriginalType.FullName;
                    var foundMethodCount = allOnCompleted.Length;
                    Logger.Instance.LogInformation("Type {typeName} was found to implement INotifyCompletion, but no suitable method was found. {foundMethodCount} method(s) were found with the required name.", typeName, foundMethodCount);
                    continue;
                }

                var onCompletedAttr = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;
                var sig = MethodSignature.CreateInstance(voidRef, [actionUntypedRef.Value.ToTypeSignature()]);

                var proxyOnCompleted = new MethodDefinition(nameof(INotifyCompletion.OnCompleted), onCompletedAttr, sig);
                var parameter = proxyOnCompleted.Parameters[0].GetOrCreateDefinition();
                parameter.Name = "continuation";

                var body = proxyOnCompleted.CilMethodBody ??= new(proxyOnCompleted);

                typeContext.NewType.Interfaces.Add(new(notifyCompletionRef.Value));
                typeContext.NewType.Methods.Add(proxyOnCompleted);

                var instructions = body.Instructions;
                instructions.Add(CilOpCodes.Ldarg_0); // load "this"
                instructions.Add(CilOpCodes.Ldarg_1); // not static, so ldarg1 loads "continuation"
                instructions.Add(CilOpCodes.Call, actionConversionRef.Value);

                // The titular jump to the interop method -- it's gotta reference the method on the right type, so we need to handle generic parameters
                // Without this, awaiters declared in generic types like UniTask<T>.Awaiter would effectively try to cast themselves to their untyped versions (UniTask<>.Awaiter in this case, which isn't a thing)
                var genericParameterCount = typeContext.NewType.GenericParameters.Count;
                if (genericParameterCount > 0)
                {
                    var typeArguments = Enumerable.Range(0, genericParameterCount).Select(i => new GenericParameterSignature(GenericParameterType.Type, i)).ToArray();
                    var interopOnCompleteGeneric = typeContext.NewType.MakeGenericInstanceType(typeArguments)
                        .ToTypeDefOrRef()
                        .CreateMemberReference(interopOnCompleted.Name, interopOnCompleted.Signature);
                    instructions.Add(CilOpCodes.Call, interopOnCompleteGeneric);
                }
                else
                {
                    instructions.Add(CilOpCodes.Call, interopOnCompleted);
                }

                instructions.Add(CilOpCodes.Ret);
            }
        }
    }
}
