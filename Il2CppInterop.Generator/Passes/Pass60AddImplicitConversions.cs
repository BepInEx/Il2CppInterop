using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass60AddImplicitConversions
{
    private const MethodAttributes OperatorAttributes = MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

    public static void DoPass(RewriteGlobalContext context)
    {
        var assemblyContext = context.GetAssemblyByName("mscorlib");
        var typeContext = assemblyContext.GetTypeByName("System.String");
        var objectTypeContext = assemblyContext.GetTypeByName("System.Object");

        var methodFromMonoString = new MethodDefinition("op_Implicit", OperatorAttributes,
            MethodSignature.CreateStatic(typeContext.NewType.ToTypeSignature(), assemblyContext.Imports.Module.String()));
        typeContext.NewType.Methods.Add(methodFromMonoString);
        methodFromMonoString.CilMethodBody = new CilMethodBody(methodFromMonoString);
        var fromBuilder = methodFromMonoString.CilMethodBody.Instructions;

        var createIl2CppStringNop = new CilInstructionLabel();

        fromBuilder.Add(OpCodes.Ldarg_0);
        fromBuilder.Add(OpCodes.Dup);
        fromBuilder.Add(OpCodes.Brtrue_S, createIl2CppStringNop);
        fromBuilder.Add(OpCodes.Ret);

        createIl2CppStringNop.Instruction = fromBuilder.Add(OpCodes.Nop);

        fromBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        fromBuilder.Add(OpCodes.Newobj,
            ReferenceCreator.CreateInstanceMethodReference(".ctor", assemblyContext.Imports.Module.Void(), typeContext.NewType, assemblyContext.Imports.Module.IntPtr()));
        fromBuilder.Add(OpCodes.Ret);

        var methodToObject = new MethodDefinition("op_Implicit", OperatorAttributes, MethodSignature.CreateStatic(objectTypeContext.NewType.ToTypeSignature()));
        methodToObject.AddParameter(assemblyContext.Imports.Module.String());
        objectTypeContext.NewType.Methods.Add(methodToObject);
        methodToObject.CilMethodBody = new CilMethodBody(methodToObject);
        var toObjectBuilder = methodToObject.CilMethodBody.Instructions;
        toObjectBuilder.Add(OpCodes.Ldarg_0);
        toObjectBuilder.Add(OpCodes.Call, methodFromMonoString);
        toObjectBuilder.Add(OpCodes.Ret);

        var methodToMonoString = new MethodDefinition("op_Implicit", OperatorAttributes, MethodSignature.CreateStatic(assemblyContext.Imports.Module.String()));
        methodToMonoString.AddParameter(typeContext.NewType.ToTypeSignature());
        typeContext.NewType.Methods.Add(methodToMonoString);
        methodToMonoString.CilMethodBody = new CilMethodBody(methodToMonoString);
        var toBuilder = methodToMonoString.CilMethodBody.Instructions;

        var createStringNop = new CilInstructionLabel();

        toBuilder.Add(OpCodes.Ldarg_0);
        toBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
        toBuilder.Add(OpCodes.Dup);
        toBuilder.Add(OpCodes.Brtrue_S, createStringNop);
        toBuilder.Add(OpCodes.Pop);
        toBuilder.Add(OpCodes.Ldnull);
        toBuilder.Add(OpCodes.Ret);

        createStringNop.Instruction = toBuilder.Add(OpCodes.Nop);
        toBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_Il2CppStringToManaged.Value);
        toBuilder.Add(OpCodes.Ret);

        AddDelegateConversions(context);

        TypeSignature[] primitiveTypes =
        [
            assemblyContext.Imports.Module.SByte(),
            assemblyContext.Imports.Module.Byte(),

            assemblyContext.Imports.Module.Short(),
            assemblyContext.Imports.Module.UShort(),

            assemblyContext.Imports.Module.Int(),
            assemblyContext.Imports.Module.UInt(),

            assemblyContext.Imports.Module.Long(),
            assemblyContext.Imports.Module.ULong(),

            assemblyContext.Imports.Module.Float(),
            assemblyContext.Imports.Module.Double(),

            assemblyContext.Imports.Module.Char(),
            assemblyContext.Imports.Module.Bool(),
        ];

        foreach (var systemType in primitiveTypes)
        {
            var il2CppSystemType = assemblyContext.GetTypeByName(systemType.FullName).NewType;

            var method = new MethodDefinition("op_Implicit", OperatorAttributes, MethodSignature.CreateStatic(objectTypeContext.NewType.ToTypeSignature()));
            method.AddParameter(systemType, "value");

            method.CilMethodBody = new CilMethodBody(method);
            var il = method.CilMethodBody.Instructions;

            var structLocal = new CilLocalVariable(il2CppSystemType.ToTypeSignature());
            method.CilMethodBody.LocalVariables.Add(structLocal);

            il.Add(OpCodes.Ldloca, structLocal);
            il.Add(OpCodes.Initobj, il2CppSystemType);

            il.Add(OpCodes.Ldloca, structLocal);
            il.Add(OpCodes.Ldarg_0);
            il.Add(OpCodes.Stfld, il2CppSystemType.Fields.Single(f => f.Name == "m_value"));

            il.Add(OpCodes.Ldloca_S, structLocal);
            il.Add(OpCodes.Call, il2CppSystemType.Methods.Single(m => m.Name == "BoxIl2CppObject"));
            il.Add(OpCodes.Ret);

            objectTypeContext.NewType.Methods.Add(method);
        }
    }

    private static void AddDelegateConversions(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                if (typeContext.OriginalType.BaseType?.FullName != "System.MulticastDelegate")
                    continue;

                var invokeMethod = typeContext.NewType.Methods.Single(it => it.Name == "Invoke");
                if (invokeMethod.Parameters.Count > 8)
                    continue; // mscorlib only contains delegates of up to 8 parameters

                // Don't generate implicit conversions for pointers and byrefs, as they can't be specified in generics
                if (invokeMethod.Parameters.Any(it => it.ParameterType is PointerTypeSignature or ByReferenceTypeSignature))
                    continue;

                var implicitMethod = new MethodDefinition("op_Implicit", OperatorAttributes, MethodSignature.CreateStatic(typeContext.SelfSubstitutedRef.ToTypeSignature()));
                typeContext.NewType.Methods.Add(implicitMethod);
                implicitMethod.CilMethodBody = new CilMethodBody(implicitMethod);

                var hasReturn = invokeMethod.Signature!.ReturnType.FullName != "System.Void";
                var hasParameters = invokeMethod.Parameters.Count > 0;

                TypeSignature monoDelegateType;
                if (!hasReturn && !hasParameters)
                    monoDelegateType = typeContext.NewType.Module!.Action();
                else if (!hasReturn)
                    monoDelegateType = typeContext.NewType.Module!.Action(invokeMethod.Parameters.Count);
                else
                    monoDelegateType = typeContext.NewType.Module!.Func(invokeMethod.Parameters.Count);

                GenericInstanceTypeSignature? genericInstanceType = null;
                if (hasParameters)
                {
                    genericInstanceType = new GenericInstanceTypeSignature(monoDelegateType.ToTypeDefOrRef(), false);
                    foreach (var t in invokeMethod.Parameters)
                        genericInstanceType.TypeArguments.Add(t.ParameterType);
                }

                if (hasReturn)
                {
                    genericInstanceType ??= new GenericInstanceTypeSignature(monoDelegateType.ToTypeDefOrRef(), false);
                    genericInstanceType.TypeArguments.Add(invokeMethod.Signature.ReturnType);
                }

                implicitMethod.AddParameter(genericInstanceType != null
                    ? typeContext.NewType.Module!.DefaultImporter.ImportTypeSignature(genericInstanceType)
                    : monoDelegateType);

                var bodyBuilder = implicitMethod.CilMethodBody.Instructions;

                bodyBuilder.Add(OpCodes.Ldarg_0);
                var delegateSupportTypeRef = typeContext.AssemblyContext.Imports.DelegateSupport;
                var genericConvertSignature = MethodSignature.CreateStatic(new GenericParameterSignature(GenericParameterType.Method, 0), 1, assemblyContext.Imports.Module.Delegate());
                var genericConvertRef = new MemberReference(delegateSupportTypeRef.ToTypeDefOrRef(), "ConvertDelegate", genericConvertSignature);
                var convertMethodRef = genericConvertRef.MakeGenericInstanceMethod(typeContext.SelfSubstitutedRef.ToTypeSignature());
                bodyBuilder.Add(OpCodes.Call, typeContext.NewType.Module!.DefaultImporter.ImportMethod(convertMethodRef));
                bodyBuilder.Add(OpCodes.Ret);

                // public static T operator+(T lhs, T rhs) => Il2CppSystem.Delegate.Combine(lhs, rhs).Cast<T>();
                var addMethod = new MethodDefinition("op_Addition", OperatorAttributes, MethodSignature.CreateStatic(typeContext.SelfSubstitutedRef.ToTypeSignature()));
                typeContext.NewType.Methods.Add(addMethod);
                addMethod.AddParameter(typeContext.SelfSubstitutedRef.ToTypeSignature());
                addMethod.AddParameter(typeContext.SelfSubstitutedRef.ToTypeSignature());
                addMethod.CilMethodBody = new CilMethodBody(addMethod);
                var addBody = addMethod.CilMethodBody.Instructions;
                addBody.Add(OpCodes.Ldarg_0);
                addBody.Add(OpCodes.Ldarg_1);
                addBody.Add(OpCodes.Call, assemblyContext.Imports.Il2CppSystemDelegateCombine.Value);
                addBody.Add(OpCodes.Call,
                    assemblyContext.Imports.Module.DefaultImporter.ImportMethod(assemblyContext.Imports.Il2CppObjectBase_Cast.Value.MakeGenericInstanceMethod(typeContext.SelfSubstitutedRef.ToTypeSignature())));
                addBody.Add(OpCodes.Ret);

                // public static T operator-(T lhs, T rhs) => Il2CppSystem.Delegate.Remove(lhs, rhs)?.Cast<T>();
                var subtractMethod = new MethodDefinition("op_Subtraction", OperatorAttributes, MethodSignature.CreateStatic(typeContext.SelfSubstitutedRef.ToTypeSignature()));
                typeContext.NewType.Methods.Add(subtractMethod);
                subtractMethod.AddParameter(typeContext.SelfSubstitutedRef.ToTypeSignature());
                subtractMethod.AddParameter(typeContext.SelfSubstitutedRef.ToTypeSignature());
                subtractMethod.CilMethodBody = new CilMethodBody(subtractMethod);
                var subtractBody = subtractMethod.CilMethodBody.Instructions;
                subtractBody.Add(OpCodes.Ldarg_0);
                subtractBody.Add(OpCodes.Ldarg_1);
                subtractBody.Add(OpCodes.Call, assemblyContext.Imports.Il2CppSystemDelegateRemove.Value);
                subtractBody.Add(OpCodes.Dup);
                var ret = new CilInstructionLabel();
                subtractBody.Add(OpCodes.Brfalse_S, ret);
                subtractBody.Add(OpCodes.Call,
                    assemblyContext.Imports.Module.DefaultImporter.ImportMethod(assemblyContext.Imports.Il2CppObjectBase_Cast.Value.MakeGenericInstanceMethod(typeContext.SelfSubstitutedRef.ToTypeSignature())));
                ret.Instruction = subtractBody.Add(OpCodes.Ret);
            }
        }
    }
}
