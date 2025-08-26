using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

internal static class MonoIl2CppConversion
{
    /// <remarks>
    /// Does not convert strings, which are treated as normal Il2Cpp objects.
    /// </remarks>
    public static bool AddIl2CppToMonoConversion(List<Instruction> instructions, TypeAnalysisContext il2CppType)
    {
        // Depending on the type of the local variable, we may need to convert it.
        // If the local variable is Pointer<T>, we need to convert it to T*.
        // If the local variable is an Il2Cpp primitive (like Il2CppSystem.Int32), we need to convert it to the corresponding C# type.
        // If the local variable is an Il2Cpp enum, we need to convert it to the underlying C# primitive type.

        if (IsIl2CppPrimitiveValueType(il2CppType))
        {
            var monoType = il2CppType.AppContext.Mscorlib.GetTypeByFullNameOrThrow($"System.{il2CppType.Name}");
            var conversionMethod = il2CppType.GetImplicitConversionTo(monoType);
            instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
            return true;
        }
        else if (il2CppType is GenericInstanceTypeAnalysisContext { GenericArguments.Count: 1, GenericType.DeclaringType: null } genericInstanceType)
        {
            if (genericInstanceType.GenericType.Name == $"{nameof(Pointer<>)}`1" && genericInstanceType.GenericType.Namespace == typeof(Pointer<>).Namespace)
            {
                var elementType = genericInstanceType.GenericArguments[0];
                var conversionMethod = genericInstanceType.GenericType.Methods.First(m => m.Name == "op_Implicit" && m.ReturnType is PointerTypeAnalysisContext);
                instructions.Add(new Instruction(OpCodes.Call, new ConcreteGenericMethodAnalysisContext(conversionMethod, [elementType], [])));
                return true;
            }
            if (genericInstanceType.GenericType.Name == $"{nameof(ByReference<>)}`1" && genericInstanceType.GenericType.Namespace == typeof(ByReference<>).Namespace)
            {
                var elementType = genericInstanceType.GenericArguments[0];
                var conversionMethod = genericInstanceType.GenericType.Methods.First(m => m.Name == nameof(ByReference<>.ToRef));
                instructions.Add(new Instruction(OpCodes.Call, new ConcreteGenericMethodAnalysisContext(conversionMethod, [elementType], [])));
                return true;
            }
        }
        else if (il2CppType.EnumMonoUnderlyingType is { } underlyingType)
        {
            var conversionMethod = il2CppType.GetExplicitConversionTo(underlyingType);
            instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
            return true;
        }
        return false;
    }

    /// <remarks>
    /// Does not convert strings, which are treated as normal Il2Cpp objects.
    /// </remarks>
    public static bool AddMonoToIl2CppConversion(List<Instruction> instructions, TypeAnalysisContext il2CppType)
    {
        if (IsIl2CppPrimitiveValueType(il2CppType))
        {
            var monoType = il2CppType.AppContext.Mscorlib.GetTypeByFullNameOrThrow($"System.{il2CppType.Name}");
            var conversionMethod = il2CppType.GetImplicitConversionFrom(monoType);
            instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
            return true;
        }
        else if (il2CppType is GenericInstanceTypeAnalysisContext { GenericArguments.Count: 1, GenericType.DeclaringType: null } genericInstanceType)
        {
            if (genericInstanceType.GenericType.Name == $"{nameof(Pointer<>)}`1" && genericInstanceType.GenericType.Namespace == typeof(Pointer<>).Namespace)
            {
                var elementType = genericInstanceType.GenericArguments[0];
                var conversionMethod = genericInstanceType.GenericType.Methods.First(m => m.Name == "op_Implicit" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType is PointerTypeAnalysisContext);
                instructions.Add(new Instruction(OpCodes.Call, new ConcreteGenericMethodAnalysisContext(conversionMethod, [elementType], [])));
                return true;
            }
            if (genericInstanceType.GenericType.Name == $"{nameof(ByReference<>)}`1" && genericInstanceType.GenericType.Namespace == typeof(ByReference<>).Namespace)
            {
                var elementType = genericInstanceType.GenericArguments[0];
                var conversionMethod = genericInstanceType.GenericType.Methods.First(m => m.Name == nameof(ByReference<>.FromRef));
                instructions.Add(new Instruction(OpCodes.Call, new ConcreteGenericMethodAnalysisContext(conversionMethod, [elementType], [])));
                return true;
            }
        }
        else if (il2CppType.EnumMonoUnderlyingType is { } underlyingType)
        {
            var conversionMethod = il2CppType.GetExplicitConversionFrom(underlyingType);
            instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
            return true;
        }
        return false;
    }

    public static void AddIl2CppToMonoStringConversion(List<Instruction> instructions, ApplicationAnalysisContext appContext)
    {
        var il2CppSystemString = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.String");
        var monoSystemString = appContext.SystemTypes.SystemStringType;
        var conversionMethod = il2CppSystemString.GetImplicitConversionTo(monoSystemString);
        instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
    }

    public static void AddMonoToIl2CppStringConversion(List<Instruction> instructions, ApplicationAnalysisContext appContext)
    {
        var il2CppSystemString = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.String");
        var monoSystemString = appContext.SystemTypes.SystemStringType;
        var conversionMethod = il2CppSystemString.GetImplicitConversionFrom(monoSystemString);
        instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
    }

    private static bool IsIl2CppPrimitiveValueType(TypeAnalysisContext type)
    {
        if (type is ReferencedTypeAnalysisContext)
            return false;

        if (type.DeclaringType is not null)
            return false;

        if (type.DeclaringAssembly != type.AppContext.Il2CppMscorlib)
            return false;

        if (type.Namespace != "Il2CppSystem")
            return false;

        return type.Name
            is "Byte"
            or "SByte"
            or "Int16"
            or "UInt16"
            or "Int32"
            or "UInt32"
            or "IntPtr"
            or "UIntPtr"
            or "Int64"
            or "UInt64"
            or "Single"
            or "Double"
            or "Boolean"
            or "Char";
    }
}
