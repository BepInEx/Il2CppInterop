using System.Runtime.CompilerServices;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass61ImplementAwaiters
{
    private class ParameterCloneListener(TypeSignature corLibAction) : MemberClonerListener
    {
        public override void OnClonedMethod(MethodDefinition original, MethodDefinition cloned)
        {
            if (cloned.Signature is not null && cloned.Signature.ParameterTypes.Count > 0)
                cloned.Signature.ParameterTypes[0] = corLibAction;

            cloned.Name = nameof(INotifyCompletion.OnCompleted); // in case it's explicitly implemented and was unhollowed as "System_Runtime_CompilerServices_INotifyCompletion_OnCompleted"
            cloned.CilMethodBody = new(cloned);
            cloned.CustomAttributes.Clear();
            original.DeclaringType?.Methods.Add(cloned);
        }
    }

    public static void DoPass(RewriteGlobalContext context)
    {
        var corlib = context.CorLib;
        var actionUntyped = corlib.GetTypeByName("System.Action");

        var actionConversion = actionUntyped.NewType.Methods.FirstOrDefault(m => m.Name == "op_Implicit") ?? throw new MissingMethodException("Untyped action conversion");

        foreach (var assemblyContext in context.Assemblies)
        {
            // Use Lazy as a lazy way to not actually import the references until they're needed

            Lazy<ITypeDefOrRef> actionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(actionConversion.Parameters[0].ParameterType.ToTypeDefOrRef())!);
            Lazy<IMethodDefOrRef> actionConversionRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportMethod(actionConversion));
            Lazy<ITypeDefOrRef> notifyCompletionRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(typeof(INotifyCompletion)));
            var voidRef = assemblyContext.NewAssembly.ManifestModule!.CorLibTypeFactory.Void;

            foreach (var typeContext in assemblyContext.Types)
            {
                // Used later for MemberCloner, just putting up here as an early exit in case .Module is ever null
                if (typeContext.NewType.Module is null)
                    continue;

                // Odds are a majority of types won't implement any interfaces. Skip them to save time.
                if (typeContext.OriginalType.IsInterface || typeContext.OriginalType.Interfaces.Count == 0)
                    continue;

                var interfaceImplementation = typeContext.OriginalType.Interfaces.FirstOrDefault(interfaceImpl => interfaceImpl.Interface?.Name == nameof(INotifyCompletion));
                if (interfaceImplementation is null)
                    continue;

                var allOnCompleted = typeContext.NewType.Methods.Where(m => m.Name == nameof(INotifyCompletion.OnCompleted)).ToArray();
                if (allOnCompleted.Length == 0)
                {
                    // Likely defined as INotifyCompletion.OnCompleted & the name is unhollowed as something like "System_Runtime_CompilerServices_INotifyCompletion_OnCompleted"
                    allOnCompleted = typeContext.NewType.Methods.Where(m => ((string?)m.Name)?.EndsWith(nameof(INotifyCompletion.OnCompleted)) ?? false).ToArray();
                    var typeName = typeContext.OriginalType.FullName;
                    Logger.Instance.LogInformation("Found explicit implementation of INotifyCompletion on {typeName}", typeName);
                }

                // Conversion spits out an Il2CppSystem.Action, so look for methods that take that (and only that) in & return void, so the stack is balanced
                // And use IsAssignableTo because otherwise equality checks would fail due to the TypeSignatures being different references
                var interopOnCompleted = allOnCompleted.FirstOrDefault(m => m.Parameters.Count == 1 && m.Signature is not null && m.Signature.ReturnType == voidRef && SignatureComparer.Default.Equals(m.Signature.ParameterTypes[0], actionConversion.Signature?.ReturnType));

                if (interopOnCompleted is null)
                {
                    var typeName = typeContext.OriginalType.FullName;
                    var foundMethodCount = allOnCompleted.Length;
                    Logger.Instance.LogInformation("Type {typeName} was found to implement INotifyCompletion, but no suitable method was found. {foundMethodCount} method(s) were found with the required name.", typeName, foundMethodCount);
                    continue;
                }

                var cloner = new MemberCloner(typeContext.NewType.Module, new ParameterCloneListener(actionUntypedRef.Value.ToTypeSignature()))
                    .Include(interopOnCompleted);
                var cloneResult = cloner.Clone();

                // Established that INotifyCompletion.OnCompleted is implemented, & interop method is defined, now clone it to create the .NET interface implementation method that jumps straight to it
                var proxyOnCompleted = (MethodDefinition)cloneResult.ClonedMembers.Single();
                proxyOnCompleted.Signature!.ParameterTypes[0] = actionUntypedRef.Value.ToTypeSignature();
                var parameter = proxyOnCompleted.Parameters[0].GetOrCreateDefinition();

                var body = proxyOnCompleted.CilMethodBody ??= new(proxyOnCompleted);

                typeContext.NewType.Interfaces.Add(new(notifyCompletionRef.Value));

                var instructions = body.Instructions;
                instructions.Add(CilOpCodes.Nop);
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
                        .CreateMemberReference(interopOnCompleted.Name!, interopOnCompleted.Signature!); // MemberReference ctor uses nullables, so we can tell the compiler "shut up I know what I'm doing"
                    instructions.Add(CilOpCodes.Call, interopOnCompleteGeneric);
                }
                else
                {
                    instructions.Add(CilOpCodes.Call, interopOnCompleted);
                }

                instructions.Add(CilOpCodes.Nop);
                instructions.Add(CilOpCodes.Ret);
            }
        }
    }
}
