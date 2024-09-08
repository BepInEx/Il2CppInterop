using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass25GenerateNonBlittableValueTypeDefaultCtors
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                if (typeContext.ComputedTypeSpecifics !=
                    TypeRewriteContext.TypeSpecifics.NonBlittableStruct) continue;

                var emptyCtor = new MethodDefinition(".ctor",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RuntimeSpecialName |
                    MethodAttributes.HideBySig, MethodSignature.CreateInstance(assemblyContext.Imports.Module.Void()));

                typeContext.NewType.Methods.Add(emptyCtor);

                emptyCtor.CilMethodBody = new(emptyCtor);

                // NOTE(Kas): This used to stackalloc data of the valuetype's size and box it into an object
                // but it seems like it caused issues on certain games. If more issues arise - revert this.
                var bodyBuilder = emptyCtor.CilMethodBody.Instructions;
                bodyBuilder.Add(OpCodes.Ldarg_0);
                bodyBuilder.Add(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
                bodyBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_object_new.Value);
                bodyBuilder.Add(OpCodes.Call,
                    new MemberReference(typeContext.NewType.BaseType, ".ctor", MethodSignature.CreateInstance(assemblyContext.Imports.Module.Void(), assemblyContext.Imports.Module.IntPtr())));
                bodyBuilder.Add(OpCodes.Ret);
            }
    }
}
