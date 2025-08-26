using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

internal sealed class TypeConversionVisitor : TypeReplacementVisitor
{
    private TypeConversionVisitor(Dictionary<TypeAnalysisContext, TypeAnalysisContext> replacements) : base(replacements)
    {
    }

    public required TypeAnalysisContext Il2CppArrayBase { get; init; }
    public required TypeAnalysisContext Pointer { get; init; }
    public required TypeAnalysisContext ByRef { get; init; }

    public static TypeConversionVisitor Create(ApplicationAnalysisContext appContext)
    {
        var il2CppMscorlib = appContext.AssembliesByName["Il2Cppmscorlib"];
        var mscorlib = appContext.AssembliesByName["mscorlib"];
        var il2CppInteropRuntime = appContext.AssembliesByName["Il2CppInterop.Runtime"];

        var il2CppArrayBase = il2CppInteropRuntime.GetTypeByFullNameOrThrow(typeof(Il2CppArrayBase<>));
        var pointer = il2CppInteropRuntime.GetTypeByFullNameOrThrow(typeof(Pointer<>));
        var byRef = il2CppInteropRuntime.GetTypeByFullNameOrThrow(typeof(ByReference<>));

        (string, string)[] replacements =
        [
            ("Il2CppSystem.Object", "Il2CppSystem.IObject"),
            ("Il2CppSystem.Enum", "Il2CppSystem.IEnum"),
            ("Il2CppSystem.ValueType", "Il2CppSystem.IValueType"),
            //("Il2CppSystem.Object", "System.Object"),
            //("Il2CppSystem.Void", "System.Void"),
            //("Il2CppSystem.Enum", "System.Enum"),
            //("Il2CppSystem.ValueType", "System.ValueType"),
            //("Il2CppSystem.Boolean", "System.Boolean"),
            //("Il2CppSystem.Char", "System.Char"),
            //("Il2CppSystem.SByte", "System.SByte"),
            //("Il2CppSystem.Byte", "System.Byte"),
            //("Il2CppSystem.Int16", "System.Int16"),
            //("Il2CppSystem.UInt16", "System.UInt16"),
            //("Il2CppSystem.Int32", "System.Int32"),
            //("Il2CppSystem.UInt32", "System.UInt32"),
            //("Il2CppSystem.Int64", "System.Int64"),
            //("Il2CppSystem.UInt64", "System.UInt64"),
            //("Il2CppSystem.Single", "System.Single"),
            //("Il2CppSystem.Double", "System.Double"),
            //("Il2CppSystem.IntPtr", "System.IntPtr"),
            //("Il2CppSystem.UIntPtr", "System.UIntPtr"),
        ];

        var replacementDictionary = replacements.ToDictionary(pair => il2CppMscorlib.GetTypeByFullNameOrThrow(pair.Item1), pair => il2CppMscorlib.GetTypeByFullNameOrThrow(pair.Item2));

        return new TypeConversionVisitor(replacementDictionary)
        {
            Il2CppArrayBase = il2CppArrayBase,
            Pointer = pointer,
            ByRef = byRef
        };
    }

    protected override TypeAnalysisContext CombineResults(ArrayTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return base.CombineResults(type, elementResult);
    }

    protected override TypeAnalysisContext CombineResults(SzArrayTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return Il2CppArrayBase.MakeGenericInstanceType([elementResult]);
    }

    protected override TypeAnalysisContext CombineResults(PointerTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return Pointer.MakeGenericInstanceType([elementResult]);
    }

    protected override TypeAnalysisContext CombineResults(ByRefTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return ByRef.MakeGenericInstanceType([elementResult]);
    }
}
