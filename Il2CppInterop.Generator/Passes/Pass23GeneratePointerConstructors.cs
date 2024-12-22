using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass23GeneratePointerConstructors
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.BlittableStruct ||
                    typeContext.OriginalType.IsEnum) continue;

                var newType = typeContext.NewType;

                var nativeCtor = new MethodDefinition(".ctor",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RuntimeSpecialName |
                    MethodAttributes.HideBySig, MethodSignature.CreateInstance(assemblyContext.Imports.Module.Void()));

                nativeCtor.AddParameter(assemblyContext.Imports.Module.IntPtr(), "pointer");

                nativeCtor.CilMethodBody = new(nativeCtor);
                var ctorBody = nativeCtor.CilMethodBody.Instructions;
                newType.Methods.Add(nativeCtor);

                ctorBody.Add(OpCodes.Ldarg_0);
                ctorBody.Add(OpCodes.Ldarg_1);
                ctorBody.Add(OpCodes.Call,
                    new MemberReference(newType.BaseType, ".ctor", MethodSignature.CreateInstance(assemblyContext.Imports.Module.Void(), assemblyContext.Imports.Module.IntPtr())));
                ctorBody.Add(OpCodes.Ret);
            }
    }
}
