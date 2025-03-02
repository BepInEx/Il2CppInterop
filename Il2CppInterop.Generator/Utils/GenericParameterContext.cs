using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.Utils;

public struct GenericParameterContext
{
    public GenericParameterContext(IHasGenericParameters? type, IHasGenericParameters? method)
    {
        this.Type = type;
        this.Method = method;
    }

    /// <summary>
    /// Gets the object responsible for providing current type arguments generic parameters
    /// </summary>
    public IHasGenericParameters? Type { get; }

    /// <summary>
    /// Gets the object responsible for providing current method generic parameters
    /// </summary>
    public IHasGenericParameters? Method { get; }

    public GenericParameter? GetGenericParameter(GenericParameterSignature signature)
    {
        IHasGenericParameters? parameterSource;
        switch (signature.ParameterType)
        {
            case GenericParameterType.Type:
                parameterSource = Type;
                break;
            case GenericParameterType.Method:
                parameterSource = Method;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (parameterSource == null) return null;

        if (signature.Index >= 0 && signature.Index < parameterSource.GenericParameters.Count)
            return parameterSource.GenericParameters[signature.Index];
        throw new ArgumentOutOfRangeException();
    }
}
