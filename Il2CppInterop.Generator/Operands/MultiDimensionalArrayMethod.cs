using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils.AsmResolver;

namespace Il2CppInterop.Generator.Operands;

public sealed record class MultiDimensionalArrayMethod(ArrayTypeAnalysisContext ArrayType, MultiDimensionalArrayMethodType MethodType)
{
    public IMethodDescriptor ToMethodDescriptor(ModuleDefinition module)
    {
        var arrayTypeSignature = ArrayType.ToTypeSignature(module);
        var elementTypeSignature = arrayTypeSignature.BaseType;
        var indexParameters = Enumerable.Repeat(module.CorLibTypeFactory.Int32, ArrayType.Rank);
        var methodSignature = MethodType switch
        {
            MultiDimensionalArrayMethodType.Get => MethodSignature.CreateInstance(elementTypeSignature, indexParameters),
            MultiDimensionalArrayMethodType.Set => MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, indexParameters.Append(elementTypeSignature)),
            MultiDimensionalArrayMethodType.Constructor => MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, indexParameters),
            MultiDimensionalArrayMethodType.Address => MethodSignature.CreateInstance(elementTypeSignature.MakeByReferenceType(), indexParameters),
            _ => throw new InvalidOperationException($"Unknown {nameof(MultiDimensionalArrayMethodType)}: {MethodType}"),
        };
        var methodName = MethodType switch
        {
            MultiDimensionalArrayMethodType.Get => "Get",
            MultiDimensionalArrayMethodType.Set => "Set",
            MultiDimensionalArrayMethodType.Constructor => ".ctor",
            MultiDimensionalArrayMethodType.Address => "Address",
            _ => null,
        };
        return new MemberReference(arrayTypeSignature.ToTypeDefOrRef(), methodName, methodSignature);
    }
}
