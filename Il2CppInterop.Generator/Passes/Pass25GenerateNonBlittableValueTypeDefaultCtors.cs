using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                    MethodAttributes.HideBySig, assemblyContext.Imports.Module.Void());

                typeContext.NewType.Methods.Add(emptyCtor);

                // NOTE(Kas): This used to stackalloc data of the valuetype's size and box it into an object
                // but it seems like it caused issues on certain games. If more issues arise - revert this.
                var bodyBuilder = emptyCtor.Body.GetILProcessor();
                bodyBuilder.Emit(OpCodes.Ldarg_0);
                bodyBuilder.Emit(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
                bodyBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_object_new.Value);
                bodyBuilder.Emit(OpCodes.Call,
                    new MethodReference(".ctor", assemblyContext.Imports.Module.Void(), typeContext.NewType.BaseType)
                    {
                        HasThis = true,
                        Parameters = { new ParameterDefinition(assemblyContext.Imports.Module.IntPtr()) }
                    });
                bodyBuilder.Emit(OpCodes.Ret);
            }
    }
}
