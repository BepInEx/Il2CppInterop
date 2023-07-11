using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass23GeneratePointerConstructors
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                if (typeContext.ComputedTypeSpecifics.IsBlittable() ||
                    typeContext.OriginalType.IsEnum) continue;

                var newType = typeContext.NewType;
                var nativeCtor = new MethodDefinition(".ctor",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                    MethodAttributes.HideBySig, assemblyContext.Imports.Module.Void());

                nativeCtor.Parameters.Add(new ParameterDefinition("pointer", ParameterAttributes.None,
                    assemblyContext.Imports.Module.IntPtr()));

                var ctorBody = nativeCtor.Body.GetILProcessor();
                newType.Methods.Add(nativeCtor);

                ctorBody.Emit(OpCodes.Ldarg_0);
                ctorBody.Emit(OpCodes.Ldarg_1);
                ctorBody.Emit(OpCodes.Call,
                    new MethodReference(".ctor", assemblyContext.Imports.Module.Void(), newType.BaseType)
                    { Parameters = { new ParameterDefinition(assemblyContext.Imports.Module.IntPtr()) }, HasThis = true });
                ctorBody.Emit(OpCodes.Ret);
            }
    }
}
