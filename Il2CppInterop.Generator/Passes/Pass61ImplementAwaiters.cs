using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using MelonLoader;
using System.Runtime.CompilerServices;

namespace Il2CppInterop.Generator.Passes;

internal static class Pass61ImplementAwaiters
{
    public static void DoPass(RewriteGlobalContext context)
    {
        AssemblyRewriteContext corlib = context.GetAssemblyByName("mscorlib");
        TypeRewriteContext actionUntyped = corlib.GetTypeByName("System.Action");

        MethodDefinition actionConversionUntyped = actionUntyped.NewType.Methods.FirstOrDefault(m => m.Name == "op_Implicit") ?? throw new MissingMethodException("Untyped action conversion");

        foreach (AssemblyRewriteContext assemblyContext in context.Assemblies)
        {

            // dont actually import the references until they're needed

            Lazy<ITypeDefOrRef> actionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(actionConversionUntyped.Parameters[0].ParameterType.ToTypeDefOrRef())!);
            Lazy<ITypeDefOrRef> interopActionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(actionUntyped.NewType)!);
            Lazy<IMethodDefOrRef> actionConversionUntypedRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportMethod(actionConversionUntyped));
            Lazy<ITypeDefOrRef> notifyCompletionRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(typeof(INotifyCompletion)));
            Lazy<ITypeDefOrRef> voidRef = new(() => assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(typeof(void)));

            foreach (TypeRewriteContext typeContext in assemblyContext.Types)
            {
                InterfaceImplementation? interfaceImplementation = typeContext.OriginalType.Interfaces.FirstOrDefault(InterfaceImplementation => InterfaceImplementation.Interface.Name == nameof(INotifyCompletion));
                if (interfaceImplementation is null)
                    continue;
                if (typeContext.OriginalType.IsInterface)
                    continue;

                bool isGeneric = typeContext.OriginalType.GenericParameters.Count > 0;

                TypeDefinition awaiterType = typeContext.OriginalType;

                MethodRewriteContext? onCompleteContext = typeContext.TryGetMethodByName(nameof(INotifyCompletion.OnCompleted));
                //System.Reflection.MethodInfo newOncompleteTyped = typeof(string).GetMethod("");
                MethodDefinition? interopOnComplete = typeContext.NewType.Methods.FirstOrDefault(m => m.Name == nameof(INotifyCompletion.OnCompleted));
                IMethodDefOrRef? interopOnCompleteRef = interopOnComplete;
                //var j = 

                if (interopOnComplete?.CilMethodBody is null)
                    continue;


                //var interopInstructions = interopOnComplete.CilMethodBody.Instructions;
                //foreach (var item in interopInstructions)
                //{
                //    // loads the static field that holds the IL2CPP method pointer
                //    if (item.OpCode.Code != CilCode.Ldsfld)
                //        continue;

                //    if (item.Operand is not MemberReference field)
                //        continue;

                //    // handles generic instance types (hopefully) (if youre seeing this comment on github, it does)
                //    if (field.DeclaringType is not TypeSpecification spec)
                //        continue;


                //    var mSig = interopOnComplete.Signature;
                //    spec.CreateMemberReference(interopOnComplete.Name!, mSig!);
                //    //interopOnComplete = spec!.Methods.FirstOrDefault(m => m.Name == nameof(INotifyCompletion.OnCompleted));
                //    //Debugger.Break();
                //}


                if (onCompleteContext is null || interopOnComplete is null)
                    continue;

                MethodAttributes onCompletedAttr = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;
                MethodSignature sig = MethodSignature.CreateInstance(voidRef.Value.ToTypeSignature(), new TypeSignature[] { actionUntypedRef.Value.ToTypeSignature() });

                #region Handle generic declaring types of nested types
                // many awaiters are nested types, so we need to handle generic declaring types
                // shit like UniTask<T>.Awaiter
                // our new OnCompletes cant just call Task<>.Awaiter.OnComplete
                //   this is probably hacky, only checking the single declaring type, but honestly, it works, i dont care.

                #endregion

                MethodDefinition proxyOnComplete = new MethodDefinition(onCompleteContext.NewMethod.Name, onCompletedAttr, sig);
                ParameterDefinition parameter = proxyOnComplete.Parameters[0].GetOrCreateDefinition();
                parameter.Name = "continuation";
                //(1, , interopOnComplete.ParameterDefinitions[0].Attributes);

                CilMethodBody body = proxyOnComplete.CilMethodBody ??= new(proxyOnComplete);

                typeContext.NewType.Interfaces.Add(new(notifyCompletionRef.Value));
                typeContext.NewType.Methods.Add(proxyOnComplete);

                CilInstructionCollection instructions = body.Instructions;
                instructions.Add(CilOpCodes.Nop);
                instructions.Add(CilOpCodes.Ldarg_0);
                instructions.Add(CilOpCodes.Ldarg_1); // ldarg1 bc not static, so ldarg0 is "this" & ldarg1 is the parameter
                instructions.Add(CilOpCodes.Call, actionConversionUntypedRef.Value);
                if (typeContext.NewType.DeclaringType?.GenericParameters.Count > 0)
                {
                    var interopOnCompleteGeneric = typeContext.NewType.MakeGenericInstanceType(false, new GenericParameterSignature(GenericParameterType.Type, 0))
                        .ToTypeDefOrRef()
                        .CreateMemberReference(interopOnComplete.Name!, interopOnComplete.Signature);
                    instructions.Add(CilOpCodes.Call, interopOnCompleteGeneric);
                }
                else
                    instructions.Add(CilOpCodes.Call, interopOnComplete);
                instructions.Add(CilOpCodes.Nop);
                instructions.Add(CilOpCodes.Ret);

                MelonLogger.Msg("Created member: " + body.Owner.FullName);
                foreach (var item in instructions)
                {
                    MelonLogger.Msg("\t" + CilInstructionFormatter.Instance.FormatInstruction(item));
                }
                //body.MaxStack = 8;
                //proxyOnComplete.CilMethodBody.ComputeMaxStackOnBuild = false;
            }
        }
    }

    //static TypeReference MakeGenericType(this TypeReference self, params TypeReference[] arguments)
    //{
    //    if (self.GenericParameters.Count != arguments.Length)
    //        throw new ArgumentException();

    //    var instance = new GenericInstanceType(self);
    //    foreach (var argument in arguments)
    //        instance.GenericArguments.Add(argument);

    //    return instance;
    //}

    //public static MethodReference MakeGeneric(this MethodReference self, TypeReference declaringType)
    //{
    //    var reference = new MethodReference(self.Name, self.ReturnType)
    //    {
    //        Name = self.Name,
    //        DeclaringType = declaringType,
    //        HasThis = self.HasThis,
    //        ExplicitThis = self.ExplicitThis,
    //        ReturnType = self.ReturnType,
    //        CallingConvention = MethodCallingConvention.Generic,
    //    };

    //    foreach (var parameter in self.Parameters)
    //        reference.Parameters.Add(new ParameterDefinition
    //        (parameter.ParameterType));

    //    foreach (var generic_parameter in self.GenericParameters)
    //        reference.GenericParameters.Add(new GenericParameter(reference));

    //    return reference;
    //}
}
