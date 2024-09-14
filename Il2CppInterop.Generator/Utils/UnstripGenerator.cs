using System.Text;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Utils;

public static class UnstripGenerator
{
    public static TypeDefinition CreateDelegateTypeForICallMethod(MethodDefinition unityMethod,
        MethodDefinition convertedMethod, RuntimeAssemblyReferences imports)
    {
        var delegateType = new TypeDefinition("", unityMethod.Name + "Delegate",
            TypeAttributes.Sealed | TypeAttributes.NestedPrivate, imports.Module.MulticastDelegate().ToTypeDefOrRef());

        var constructor = new MethodDefinition(".ctor",
            MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RuntimeSpecialName |
            MethodAttributes.Public, MethodSignature.CreateInstance(imports.Module.Void(), imports.Module.Object(), imports.Module.IntPtr()));
        constructor.ImplAttributes = MethodImplAttributes.CodeTypeMask;
        delegateType.Methods.Add(constructor);

        var invokeMethod = new MethodDefinition("Invoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            MethodSignature.CreateInstance(imports.Module.Void())); // todo
        invokeMethod.ImplAttributes = MethodImplAttributes.CodeTypeMask;
        delegateType.Methods.Add(invokeMethod);

        invokeMethod.Signature!.ReturnType = convertedMethod.Signature!.ReturnType.IsValueType
            ? convertedMethod.Signature.ReturnType
            : imports.Module.IntPtr();
        if (!convertedMethod.IsStatic)
            invokeMethod.AddParameter(imports.Module.IntPtr(), "@this");
        foreach (var convertedParameter in convertedMethod.Parameters)
            invokeMethod.AddParameter(
                convertedParameter.ParameterType.IsValueType
                    ? convertedParameter.ParameterType
                    : imports.Module.IntPtr(),
                convertedParameter.Name,
                convertedParameter.Definition!.Attributes & ~ParameterAttributes.Optional);

        return delegateType;
    }

    public static void GenerateInvokerMethodBody(MethodDefinition newMethod, FieldDefinition delegateField,
        TypeDefinition delegateType, TypeRewriteContext enclosingType, RuntimeAssemblyReferences imports)
    {
        var body = newMethod.CilMethodBody!.Instructions;

        body.Add(OpCodes.Ldsfld, delegateField);
        if (!newMethod.IsStatic)
        {
            body.Add(OpCodes.Ldarg_0);
            body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
        }

        var argOffset = newMethod.IsStatic ? 0 : 1;

        for (var i = 0; i < newMethod.Parameters.Count; i++)
        {
            var param = newMethod.Parameters[i];
            var paramType = param.ParameterType;
            if (paramType.IsValueType || (paramType is ByReferenceTypeSignature && paramType.GetElementType().IsValueType))
            {
                body.AddLoadArgument(i + argOffset);
            }
            else
            {
                body.EmitObjectToPointer(param.ParameterType, param.ParameterType, enclosingType, i + argOffset, false,
                    true, true, true, out var refVar);
                if (refVar != null)
                {
                    Logger.Instance.LogTrace("Method {NewMethod} has a reference-typed ref parameter, this will be ignored",
                        newMethod.ToString());
                }
            }
        }

        body.Add(OpCodes.Call, delegateType.Methods.Single(it => it.Name == "Invoke"));
        if (!newMethod.Signature!.ReturnType.IsValueTypeLike())
        {
            var pointerVar = new CilLocalVariable(imports.Module.IntPtr());
            newMethod.CilMethodBody.LocalVariables.Add(pointerVar);
            body.Add(OpCodes.Stloc, pointerVar);
            body.EmitPointerToObject(newMethod.Signature.ReturnType, newMethod.Signature.ReturnType, enclosingType, pointerVar, false,
                false);
        }

        body.Add(OpCodes.Ret);
    }

    public static FieldDefinition GenerateStaticCtorSuffix(TypeDefinition enclosingType, TypeDefinition delegateType,
        MethodDefinition unityMethod, RuntimeAssemblyReferences imports)
    {
        var delegateField = new FieldDefinition(delegateType.Name + "Field",
            FieldAttributes.Static | FieldAttributes.Private | FieldAttributes.InitOnly, new FieldSignature(delegateType.ToTypeSignature()));
        enclosingType.Fields.Add(delegateField);

        var staticCtor = enclosingType.GetOrCreateStaticConstructor();

        var bodyProcessor = staticCtor.CilMethodBody!.Instructions;

        bodyProcessor.Remove(staticCtor.CilMethodBody.Instructions.Last()); // remove ret

        bodyProcessor.Add(OpCodes.Ldstr, GetICallSignature(unityMethod));

        var methodRef = imports.IL2CPP_ResolveICall.Value.MakeGenericInstanceMethod(delegateType.ToTypeSignature());
        bodyProcessor.Add(OpCodes.Call, enclosingType.Module!.DefaultImporter.ImportMethod(methodRef));
        bodyProcessor.Add(OpCodes.Stsfld, delegateField);

        bodyProcessor.Add(OpCodes.Ret); // restore ret

        return delegateField;
    }

    private static string GetICallSignature(MethodDefinition unityMethod)
    {
        var builder = new StringBuilder();
        builder.Append(unityMethod.DeclaringType!.FullName);
        builder.Append("::");
        builder.Append(unityMethod.Name);

        return builder.ToString();
    }
}
