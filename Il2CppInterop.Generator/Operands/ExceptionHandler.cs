using AsmResolver.DotNet.Code.Cil;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator.Operands;

public sealed record class ExceptionHandler
{
    public CilExceptionHandlerType HandlerType { get; set; }
    public ILabel? TryStart { get; set; }
    public ILabel? TryEnd { get; set; }
    public ILabel? HandlerStart { get; set; }
    public ILabel? HandlerEnd { get; set; }
    public ILabel? FilterStart { get; set; }
    public TypeAnalysisContext? ExceptionType { get; set; }
}
