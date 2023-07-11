using System.Collections.Generic;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass50GenerateMethods
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                foreach (var methodRewriteContext in typeContext.Methods)
                {
                    var originalMethod = methodRewriteContext.OriginalMethod;
                    var newMethod = methodRewriteContext.NewMethod;
                    var imports = assemblyContext.Imports;

                    var bodyBuilder = newMethod.Body.GetILProcessor();
                    var exceptionLocal = new VariableDefinition(imports.Module.IntPtr());
                    var argArray = new VariableDefinition(new PointerType(imports.Module.IntPtr()));
                    var resultVar = new VariableDefinition(imports.Module.IntPtr());
                    var valueTypeLocal = new VariableDefinition(newMethod.ReturnType);
                    newMethod.Body.Variables.Add(exceptionLocal);
                    newMethod.Body.Variables.Add(argArray);
                    newMethod.Body.Variables.Add(resultVar);

                    if (valueTypeLocal.VariableType.FullName != "System.Void")
                        newMethod.Body.Variables.Add(valueTypeLocal);

                    // Pre-initialize any present params
                    // TODO: This doesn't account for params T[] (i.e. generic element type) yet; may emit incorrect IL
                    // TODO: Do we really need a loop here? C# allows only one params array.
                    //       On the other hand, CreateParamsMethod accommodates multiple ParamArrayAttribute as well
                    Instruction nextInstruction = null;
                    for (var paramIndex = 0; paramIndex < originalMethod.Parameters.Count; paramIndex++)
                    {
                        var newParameter = newMethod.Parameters[paramIndex];
                        var originalParameter = originalMethod.Parameters[paramIndex];
                        if (!originalParameter.IsParamsArray())
                            continue;

                        var originalElementType = ((ArrayType)originalParameter.ParameterType).ElementType;

                        if (nextInstruction != null)
                            bodyBuilder.Append(nextInstruction);
                        nextInstruction = bodyBuilder.Create(OpCodes.Nop);

                        bodyBuilder.Emit(OpCodes.Ldarg, newParameter);
                        bodyBuilder.Emit(OpCodes.Brtrue, nextInstruction);

                        bodyBuilder.Emit(OpCodes.Ldc_I4_0);
                        bodyBuilder.Emit(OpCodes.Conv_I8);
                        bodyBuilder.Emit(OpCodes.Newobj, imports.Module.ImportReference(originalElementType.FullName switch
                        {
                            "System.String" => imports.Il2CppStringArrayctor_size.Value,
                            _ when originalElementType.IsValueType => imports.Il2CppStructArrayctor_size.Get(((GenericInstanceType)newParameter.ParameterType).GenericArguments[0]),
                            _ => imports.Il2CppRefrenceArrayctor_size.Get(((GenericInstanceType)newParameter.ParameterType).GenericArguments[0])
                        }));
                        bodyBuilder.Emit(OpCodes.Starg, newParameter);
                    }

                    if (nextInstruction != null)
                        bodyBuilder.Append(nextInstruction);

                    if (typeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.BlittableStruct &&
                        typeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
                    {
                        if (originalMethod.IsConstructor)
                        {
                            bodyBuilder.Emit(OpCodes.Ldarg_0);
                            bodyBuilder.Emit(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
                            bodyBuilder.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_new.Value);
                            bodyBuilder.Emit(OpCodes.Call,
                                new MethodReference(".ctor", imports.Module.Void(), typeContext.SelfSubstitutedRef)
                                { Parameters = { new ParameterDefinition(imports.Module.IntPtr()) }, HasThis = true });
                        }
                        else if (!originalMethod.IsStatic)
                        {
                            bodyBuilder.Emit(OpCodes.Ldarg_0);
                            bodyBuilder.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
                            bodyBuilder.Emit(OpCodes.Pop);
                        }
                    }

                    if (originalMethod.Parameters.Count == 0)
                    {
                        bodyBuilder.Emit(OpCodes.Ldc_I4_0);
                        bodyBuilder.Emit(OpCodes.Conv_U);
                    }
                    else
                    {
                        bodyBuilder.EmitLdcI4(originalMethod.Parameters.Count);
                        bodyBuilder.Emit(OpCodes.Conv_U);
                        bodyBuilder.Emit(OpCodes.Sizeof, imports.Module.IntPtr());
                        bodyBuilder.Emit(OpCodes.Mul_Ovf_Un);
                        bodyBuilder.Emit(OpCodes.Localloc);
                    }

                    bodyBuilder.Emit(OpCodes.Stloc, argArray);

                    var argOffset = originalMethod.IsStatic ? 0 : 1;

                    var byRefParams = new List<(int, VariableDefinition)>();

                    for (var i = 0; i < newMethod.Parameters.Count; i++)
                    {
                        bodyBuilder.Emit(OpCodes.Ldloc, argArray);
                        if (i > 0)
                        {
                            bodyBuilder.EmitLdcI4(i);
                            bodyBuilder.Emit(OpCodes.Conv_U);
                            bodyBuilder.Emit(OpCodes.Sizeof, imports.Module.IntPtr());
                            bodyBuilder.Emit(OpCodes.Mul_Ovf_Un);
                            bodyBuilder.Emit(OpCodes.Add);
                        }

                        var newParam = newMethod.Parameters[i];
                        // NOTE(Kas): out parameters of value type are passed directly as a pointer to the il2cpp method
                        // since we don't need to perform any additional copies
                        if (newParam.IsOut && !newParam.ParameterType.GetElementType().IsValueType)
                        {
                            var elementType = newParam.ParameterType.GetElementType();

                            // Storage for the output Il2CppObjectBase pointer, it's
                            // unused if there's a generic value type parameter
                            var outVar = new VariableDefinition(imports.Module.IntPtr());
                            bodyBuilder.Body.Variables.Add(outVar);

                            if (elementType.IsGenericParameter)
                            {
                                bodyBuilder.Emit(OpCodes.Ldtoken, elementType);
                                bodyBuilder.Emit(OpCodes.Call, imports.Module.TypeGetTypeFromHandle());
                                bodyBuilder.Emit(OpCodes.Callvirt, imports.Module.TypeGetIsValueType());

                                var valueTypeBlock = bodyBuilder.Create(OpCodes.Nop);
                                var continueBlock = bodyBuilder.Create(OpCodes.Nop);

                                bodyBuilder.Emit(OpCodes.Brtrue, valueTypeBlock);

                                // The generic parameter is an Il2CppObjectBase => set the output storage to a nullptr
                                bodyBuilder.EmitLdcI4(0);
                                bodyBuilder.Emit(OpCodes.Stloc, outVar);
                                bodyBuilder.Emit(OpCodes.Ldloca, outVar);
                                bodyBuilder.Emit(OpCodes.Conv_I);

                                bodyBuilder.Emit(OpCodes.Br_S, continueBlock);

                                // Instruction block that handles generic value types, we only need to return a reference
                                // to the output argument since it is already allocated for us
                                bodyBuilder.Append(valueTypeBlock);
                                bodyBuilder.Emit(OpCodes.Ldarg, argOffset + i);

                                bodyBuilder.Append(continueBlock);
                            }
                            else
                            {
                                bodyBuilder.EmitLdcI4(0);
                                bodyBuilder.Emit(OpCodes.Stloc, outVar);
                                bodyBuilder.Emit(OpCodes.Ldloca, outVar);
                                bodyBuilder.Emit(OpCodes.Conv_I);
                            }
                            byRefParams.Add((i, outVar));
                        }
                        else
                        {
                            bodyBuilder.EmitObjectToPointer(originalMethod.Parameters[i].ParameterType, newParam.ParameterType,
                                methodRewriteContext.DeclaringType, argOffset + i, false, true, true, false, out var refVar);
                            if (refVar != null)
                                byRefParams.Add((i, refVar));
                        }
                        bodyBuilder.Emit(OpCodes.Stind_I);

                    }

                    if (!originalMethod.DeclaringType.IsSealed && !originalMethod.IsFinal &&
                        ((originalMethod.IsVirtual && !originalMethod.DeclaringType.IsValueType) || originalMethod.IsAbstract))
                    {
                        bodyBuilder.Emit(OpCodes.Ldarg_0);
                        bodyBuilder.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
                        if (methodRewriteContext.GenericInstantiationsStoreSelfSubstRef != null)
                            bodyBuilder.Emit(OpCodes.Ldsfld,
                                new FieldReference("Pointer", imports.Module.IntPtr(),
                                    methodRewriteContext.GenericInstantiationsStoreSelfSubstMethodRef));
                        else
                            bodyBuilder.Emit(OpCodes.Ldsfld, methodRewriteContext.NonGenericMethodInfoPointerField);
                        bodyBuilder.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_virtual_method.Value);
                    }
                    else if (methodRewriteContext.GenericInstantiationsStoreSelfSubstRef != null)
                    {
                        bodyBuilder.Emit(OpCodes.Ldsfld,
                            new FieldReference("Pointer", imports.Module.IntPtr(),
                                methodRewriteContext.GenericInstantiationsStoreSelfSubstMethodRef));
                    }
                    else
                    {
                        bodyBuilder.Emit(OpCodes.Ldsfld, methodRewriteContext.NonGenericMethodInfoPointerField);
                    }

                    if (originalMethod.IsStatic)
                        bodyBuilder.Emit(OpCodes.Ldc_I4_0);
                    else
                        bodyBuilder.EmitObjectToPointer(originalMethod.DeclaringType, newMethod.DeclaringType, typeContext, 0,
                            true, false, true, true, out _);

                    bodyBuilder.Emit(OpCodes.Ldloc, argArray);
                    bodyBuilder.Emit(OpCodes.Ldloca, exceptionLocal);
                    bodyBuilder.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_runtime_invoke.Value);
                    bodyBuilder.Emit(OpCodes.Stloc, resultVar);

                    bodyBuilder.Emit(OpCodes.Ldloc, exceptionLocal);
                    bodyBuilder.Emit(OpCodes.Call, imports.Il2CppException_RaiseExceptionIfNecessary.Value);

                    foreach (var byRefParam in byRefParams)
                    {
                        var paramIndex = byRefParam.Item1;
                        var paramVariable = byRefParam.Item2;
                        var methodParam = newMethod.Parameters[paramIndex];

                        if (methodParam.IsOut && methodParam.ParameterType.GetElementType().IsGenericParameter)
                        {
                            bodyBuilder.Emit(OpCodes.Ldtoken, methodParam.ParameterType.GetElementType());
                            bodyBuilder.Emit(OpCodes.Call, imports.Module.TypeGetTypeFromHandle());
                            bodyBuilder.Emit(OpCodes.Callvirt, imports.Module.TypeGetIsValueType());

                            var continueBlock = bodyBuilder.Create(OpCodes.Nop);

                            bodyBuilder.Emit(OpCodes.Brtrue, continueBlock);

                            // The generic parameter is an Il2CppObjectBase => update the reference appropriately
                            bodyBuilder.EmitUpdateRef(newMethod.Parameters[paramIndex], paramIndex + argOffset, paramVariable,
                                imports);

                            bodyBuilder.Emit(OpCodes.Br_S, continueBlock);

                            // There is no need to handle generic value types, they are already passed by reference

                            bodyBuilder.Append(continueBlock);
                        }
                        else
                        {
                            bodyBuilder.EmitUpdateRef(newMethod.Parameters[paramIndex], paramIndex + argOffset, paramVariable,
                                imports);
                        }
                    }

                    bodyBuilder.EmitPointerToObject(originalMethod.ReturnType, newMethod.ReturnType, typeContext,
                        bodyBuilder.Create(OpCodes.Ldloc, resultVar), false, true);

                    bodyBuilder.Emit(OpCodes.Ret);
                }
    }
}
