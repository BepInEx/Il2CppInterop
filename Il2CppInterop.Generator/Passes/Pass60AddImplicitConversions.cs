using System.Linq;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass60AddImplicitConversions
{
    private const MethodAttributes OperatorAttributes = MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

    public static void DoPass(RewriteGlobalContext context)
    {
        var assemblyContext = context.GetAssemblyByName("mscorlib");
        var typeContext = assemblyContext.GetTypeByName("System.String");
        var objectTypeContext = assemblyContext.GetTypeByName("System.Object");

        var methodFromMonoString = new MethodDefinition("op_Implicit", OperatorAttributes, typeContext.NewType);
        methodFromMonoString.Parameters.Add(new ParameterDefinition(assemblyContext.Imports.Module.String()));
        typeContext.NewType.Methods.Add(methodFromMonoString);
        var fromBuilder = methodFromMonoString.Body.GetILProcessor();

        var createIl2CppStringNop = fromBuilder.Create(OpCodes.Nop);

        fromBuilder.Emit(OpCodes.Ldarg_0);
        fromBuilder.Emit(OpCodes.Dup);
        fromBuilder.Emit(OpCodes.Brtrue_S, createIl2CppStringNop);
        fromBuilder.Emit(OpCodes.Ret);

        fromBuilder.Append(createIl2CppStringNop);
        fromBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        fromBuilder.Emit(OpCodes.Newobj,
            new MethodReference(".ctor", assemblyContext.Imports.Module.Void(), typeContext.NewType)
            {
                HasThis = true,
                Parameters = { new ParameterDefinition(assemblyContext.Imports.Module.IntPtr()) }
            });
        fromBuilder.Emit(OpCodes.Ret);

        var methodToObject = new MethodDefinition("op_Implicit", OperatorAttributes, objectTypeContext.NewType);
        methodToObject.Parameters.Add(new ParameterDefinition(assemblyContext.Imports.Module.String()));
        objectTypeContext.NewType.Methods.Add(methodToObject);
        var toObjectBuilder = methodToObject.Body.GetILProcessor();
        toObjectBuilder.Emit(OpCodes.Ldarg_0);
        toObjectBuilder.Emit(OpCodes.Call, methodFromMonoString);
        toObjectBuilder.Emit(OpCodes.Ret);

        var methodToMonoString = new MethodDefinition("op_Implicit", OperatorAttributes, assemblyContext.Imports.Module.String());
        methodToMonoString.Parameters.Add(new ParameterDefinition(typeContext.NewType));
        typeContext.NewType.Methods.Add(methodToMonoString);
        var toBuilder = methodToMonoString.Body.GetILProcessor();

        var createStringNop = toBuilder.Create(OpCodes.Nop);

        toBuilder.Emit(OpCodes.Ldarg_0);
        toBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
        toBuilder.Emit(OpCodes.Dup);
        toBuilder.Emit(OpCodes.Brtrue_S, createStringNop);
        toBuilder.Emit(OpCodes.Pop);
        toBuilder.Emit(OpCodes.Ldnull);
        toBuilder.Emit(OpCodes.Ret);

        toBuilder.Append(createStringNop);
        toBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_Il2CppStringToManaged.Value);
        toBuilder.Emit(OpCodes.Ret);

        AddDelegateConversions(context);

        var primitiveTypes = new[]
        {
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
        };

        foreach (var systemType in primitiveTypes)
        {
            var il2CppSystemType = assemblyContext.GetTypeByName(systemType.FullName).NewType;

            var method = new MethodDefinition("op_Implicit", OperatorAttributes, objectTypeContext.NewType);
            method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, systemType));

            var il = method.Body.GetILProcessor();

            var structLocal = new VariableDefinition(il2CppSystemType);
            method.Body.Variables.Add(structLocal);

            il.Emit(OpCodes.Ldloca, structLocal);
            il.Emit(OpCodes.Initobj, il2CppSystemType);

            il.Emit(OpCodes.Ldloca, structLocal);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stfld, il2CppSystemType.Fields.Single(f => f.Name == "m_value"));

            il.Emit(OpCodes.Ldloca_S, structLocal);
            il.Emit(OpCodes.Call, il2CppSystemType.Methods.Single(m => m.Name == "BoxIl2CppObject"));
            il.Emit(OpCodes.Ret);

            objectTypeContext.NewType.Methods.Add(method);
        }
    }

    private static void AddDelegateConversions(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                if (typeContext.OriginalType.BaseType?.FullName != "System.MulticastDelegate") continue;

                var invokeMethod = typeContext.NewType.Methods.Single(it => it.Name == "Invoke");
                if (invokeMethod.Parameters.Count > 8) continue; // mscorlib only contains delegates of up to 8 parameters

                // Don't generate implicit conversions for pointers and byrefs, as they can't be specified in generics
                if (invokeMethod.Parameters.Any(it => it.ParameterType.IsByReference || it.ParameterType.IsPointer))
                    continue;

                var implicitMethod = new MethodDefinition("op_Implicit", OperatorAttributes, typeContext.SelfSubstitutedRef);
                typeContext.NewType.Methods.Add(implicitMethod);

                var hasReturn = invokeMethod.ReturnType.FullName != "System.Void";
                var hasParameters = invokeMethod.Parameters.Count > 0;

                TypeReference monoDelegateType;
                if (!hasReturn && !hasParameters)
                    monoDelegateType =
                        typeContext.NewType.Module.Action();
                else if (!hasReturn)
                    monoDelegateType =
                        typeContext.NewType.Module.Action(invokeMethod.Parameters.Count);
                else
                    monoDelegateType =
                        typeContext.NewType.Module.Func(invokeMethod.Parameters.Count);

                GenericInstanceType genericInstanceType = null;
                if (hasParameters)
                {
                    genericInstanceType = new GenericInstanceType(monoDelegateType);
                    foreach (var t in invokeMethod.Parameters)
                        genericInstanceType.GenericArguments.Add(t.ParameterType);
                }

                if (hasReturn)
                {
                    genericInstanceType ??= new GenericInstanceType(monoDelegateType);
                    genericInstanceType.GenericArguments.Add(invokeMethod.ReturnType);
                }

                implicitMethod.Parameters.Add(new ParameterDefinition(genericInstanceType != null
                    ? typeContext.NewType.Module.ImportReference(genericInstanceType)
                    : monoDelegateType));

                var bodyBuilder = implicitMethod.Body.GetILProcessor();

                bodyBuilder.Emit(OpCodes.Ldarg_0);
                var delegateSupportTypeRef = typeContext.AssemblyContext.Imports.DelegateSupport;
                var genericConvertRef =
                    new MethodReference("ConvertDelegate", assemblyContext.Imports.Module.Void(), delegateSupportTypeRef)
                    {
                        HasThis = false,
                        Parameters = { new ParameterDefinition(assemblyContext.Imports.Module.Delegate()) }
                    };
                genericConvertRef.GenericParameters.Add(new GenericParameter(genericConvertRef));
                genericConvertRef.ReturnType = genericConvertRef.GenericParameters[0];
                var convertMethodRef = new GenericInstanceMethod(genericConvertRef)
                { GenericArguments = { typeContext.SelfSubstitutedRef } };
                bodyBuilder.Emit(OpCodes.Call, typeContext.NewType.Module.ImportReference(convertMethodRef));
                bodyBuilder.Emit(OpCodes.Ret);

                // public static T operator+(T lhs, T rhs) => Il2CppSystem.Delegate.Combine(lhs, rhs).Cast<T>();
                var addMethod = new MethodDefinition("op_Addition", OperatorAttributes, typeContext.SelfSubstitutedRef);
                typeContext.NewType.Methods.Add(addMethod);
                addMethod.Parameters.Add(new ParameterDefinition(typeContext.SelfSubstitutedRef));
                addMethod.Parameters.Add(new ParameterDefinition(typeContext.SelfSubstitutedRef));
                var addBody = addMethod.Body.GetILProcessor();
                addBody.Emit(OpCodes.Ldarg_0);
                addBody.Emit(OpCodes.Ldarg_1);
                addBody.Emit(OpCodes.Call, assemblyContext.Imports.Il2CppSystemDelegateCombine.Value);
                addBody.Emit(OpCodes.Call,
                    assemblyContext.Imports.Module.ImportReference(
                        new GenericInstanceMethod(assemblyContext.Imports.Il2CppObjectBase_Cast.Value)
                        { GenericArguments = { typeContext.SelfSubstitutedRef } }));
                addBody.Emit(OpCodes.Ret);

                // public static T operator-(T lhs, T rhs) => Il2CppSystem.Delegate.Remove(lhs, rhs)?.Cast<T>();
                var subtractMethod = new MethodDefinition("op_Subtraction", OperatorAttributes, typeContext.SelfSubstitutedRef);
                typeContext.NewType.Methods.Add(subtractMethod);
                subtractMethod.Parameters.Add(new ParameterDefinition(typeContext.SelfSubstitutedRef));
                subtractMethod.Parameters.Add(new ParameterDefinition(typeContext.SelfSubstitutedRef));
                var subtractBody = subtractMethod.Body.GetILProcessor();
                subtractBody.Emit(OpCodes.Ldarg_0);
                subtractBody.Emit(OpCodes.Ldarg_1);
                subtractBody.Emit(OpCodes.Call, assemblyContext.Imports.Il2CppSystemDelegateRemove.Value);
                subtractBody.Emit(OpCodes.Dup);
                var ret = subtractBody.Create(OpCodes.Ret);
                subtractBody.Emit(OpCodes.Brfalse_S, ret);
                subtractBody.Emit(OpCodes.Call,
                    assemblyContext.Imports.Module.ImportReference(
                        new GenericInstanceMethod(assemblyContext.Imports.Il2CppObjectBase_Cast.Value)
                        { GenericArguments = { typeContext.SelfSubstitutedRef } }));
                subtractBody.Append(ret);
            }
    }
}
