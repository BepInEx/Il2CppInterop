using System.Runtime.CompilerServices;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;

namespace Il2CppInterop.Generator.Passes;

public static class Pass61ImplementAwaiters
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var corlib = context.CorLib;
        var actionUntyped = corlib.GetTypeByName("System.Action");

        var actionConversionUntyped = actionUntyped.NewType.Methods.FirstOrDefault(m => m.Name == "op_Implicit") ?? throw new MissingMethodException("Untyped action conversion");

        foreach (var assemblyContext in context.Assemblies)
        {
            // Use Lazy as a lazy way to not actually import the references until they're needed

            Lazy<ITypeDefOrRef> actionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(actionConversionUntyped.Parameters[0].ParameterType.ToTypeDefOrRef())!);
            Lazy<IMethodDefOrRef> actionConversionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportMethod(actionConversionUntyped));
            Lazy<ITypeDefOrRef> notifyCompletionRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(typeof(INotifyCompletion)));
            Lazy<ITypeDefOrRef> voidRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(typeof(void)));

            foreach (var typeContext in assemblyContext.Types)
            {
                var interfaceImplementation = typeContext.OriginalType.Interfaces.FirstOrDefault(InterfaceImplementation => InterfaceImplementation.Interface?.Name == nameof(INotifyCompletion));
                if (interfaceImplementation is null || typeContext.OriginalType.IsInterface)
                    continue;

                var onCompletedContext = typeContext.TryGetMethodByName(nameof(INotifyCompletion.OnCompleted));
                var interopOnCompleted = typeContext.NewType.Methods.FirstOrDefault(m => m.Name == nameof(INotifyCompletion.OnCompleted));
                IMethodDefOrRef? interopOnCompletedRef = interopOnCompleted;

                if (interopOnCompleted?.CilMethodBody is null || onCompletedContext is null || interopOnCompleted is null)
                    continue;

                // Established that INotifyCompletion.OnCompleted is implemented, & interop method is defined, now create the .NET interface implementation method that jumps to the proxy
                var onCompletedAttr = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;
                var sig = MethodSignature.CreateInstance(voidRef.Value.ToTypeSignature(), [actionUntypedRef.Value.ToTypeSignature()]);

                var proxyOnCompleted = new MethodDefinition(onCompletedContext.NewMethod.Name, onCompletedAttr, sig);
                var parameter = proxyOnCompleted.Parameters[0].GetOrCreateDefinition();
                parameter.Name = "continuation";

                var body = proxyOnCompleted.CilMethodBody ??= new(proxyOnCompleted);

                typeContext.NewType.Interfaces.Add(new(notifyCompletionRef.Value));
                typeContext.NewType.Methods.Add(proxyOnCompleted);

                var instructions = body.Instructions;
                instructions.Add(CilOpCodes.Nop);
                instructions.Add(CilOpCodes.Ldarg_0); // load "this"
                instructions.Add(CilOpCodes.Ldarg_1); // not static, so ldarg1 loads "continuation"
                instructions.Add(CilOpCodes.Call, actionConversionUntypedRef.Value);

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
