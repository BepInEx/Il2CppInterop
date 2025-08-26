using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Il2CppInterop.Generator;

public class TypeAnalysisContextEqualityComparer : IEqualityComparer<TypeAnalysisContext>, IEqualityComparer<GenericInstanceTypeAnalysisContext>, IEqualityComparer<GenericParameterTypeAnalysisContext>, IEqualityComparer<SentinelTypeAnalysisContext>, IEqualityComparer<CustomModifierTypeAnalysisContext>, IEqualityComparer<PinnedTypeAnalysisContext>, IEqualityComparer<PointerTypeAnalysisContext>, IEqualityComparer<ByRefTypeAnalysisContext>, IEqualityComparer<BoxedTypeAnalysisContext>, IEqualityComparer<SzArrayTypeAnalysisContext>, IEqualityComparer<ArrayTypeAnalysisContext>
{
    public static TypeAnalysisContextEqualityComparer Instance { get; } = new();

    public bool Equals(TypeAnalysisContext? x, TypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;
        if (x.Type != y.Type)
            return false;

        return x.Type switch
        {
            Il2CppTypeEnum.IL2CPP_TYPE_ARRAY => Equals(x as ArrayTypeAnalysisContext, y as ArrayTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY => Equals(x as SzArrayTypeAnalysisContext, y as SzArrayTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST => Equals(x as GenericInstanceTypeAnalysisContext, y as GenericInstanceTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_VAR or Il2CppTypeEnum.IL2CPP_TYPE_MVAR => Equals(x as GenericParameterTypeAnalysisContext, y as GenericParameterTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_SENTINEL => Equals(x as SentinelTypeAnalysisContext, y as SentinelTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_PINNED => Equals(x as PinnedTypeAnalysisContext, y as PinnedTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_PTR => Equals(x as PointerTypeAnalysisContext, y as PointerTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_BYREF => Equals(x as ByRefTypeAnalysisContext, y as ByRefTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_BOXED => Equals(x as BoxedTypeAnalysisContext, y as BoxedTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_CMOD_OPT or Il2CppTypeEnum.IL2CPP_TYPE_CMOD_REQD => Equals(x as CustomModifierTypeAnalysisContext, y as CustomModifierTypeAnalysisContext),
            Il2CppTypeEnum.IL2CPP_TYPE_FNPTR => false,// Function pointers are not part of the Cpp2IL context system
            _ => false,// Type definitions have unique instances
        };
    }
    public bool Equals(GenericInstanceTypeAnalysisContext? x, GenericInstanceTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.GenericType, y.GenericType) && x.GenericArguments.SequenceEqual(y.GenericArguments, this);
    }
    public bool Equals(GenericParameterTypeAnalysisContext? x, GenericParameterTypeAnalysisContext? y)
    {
        return ReferenceEquals(x, y); // Generic parameters have unique instances
    }
    public bool Equals(SentinelTypeAnalysisContext? x, SentinelTypeAnalysisContext? y)
    {
        if (x is null || y is null)
            return ReferenceEquals(x, y);

        return true; // Sentinel types are always equal
    }
    public bool Equals(CustomModifierTypeAnalysisContext? x, CustomModifierTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType) && Equals(x.ModifierType, y.ModifierType);
    }
    public bool Equals(PointerTypeAnalysisContext? x, PointerTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType);
    }
    public bool Equals(ByRefTypeAnalysisContext? x, ByRefTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType);
    }
    public bool Equals(BoxedTypeAnalysisContext? x, BoxedTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType);
    }
    public bool Equals(SzArrayTypeAnalysisContext? x, SzArrayTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType);
    }
    public bool Equals(ArrayTypeAnalysisContext? x, ArrayTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType) && x.Rank == y.Rank;
    }
    public bool Equals(PinnedTypeAnalysisContext? x, PinnedTypeAnalysisContext? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return Equals(x.ElementType, y.ElementType);
    }

    public int GetHashCode(TypeAnalysisContext type) => type.Type switch
    {
        Il2CppTypeEnum.IL2CPP_TYPE_ARRAY => GetHashCode((ArrayTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY => GetHashCode((SzArrayTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST => GetHashCode((GenericInstanceTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_VAR or Il2CppTypeEnum.IL2CPP_TYPE_MVAR => GetHashCode((GenericParameterTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_SENTINEL => GetHashCode((SentinelTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_PINNED => GetHashCode((PinnedTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_PTR => GetHashCode((PointerTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_BYREF => GetHashCode((ByRefTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_BOXED => GetHashCode((BoxedTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_CMOD_OPT or Il2CppTypeEnum.IL2CPP_TYPE_CMOD_REQD => GetHashCode((CustomModifierTypeAnalysisContext)type),
        Il2CppTypeEnum.IL2CPP_TYPE_FNPTR => 0, // Function pointers are not part of the Cpp2IL context system
        _ => HashCode.Combine(type.Type, type), // Type definitions have unique instances
    };

    public int GetHashCode(GenericInstanceTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.GenericType, this);
        foreach (var genericArgument in type.GenericArguments)
        {
            hash.Add(genericArgument, this);
        }
        return hash.ToHashCode();
    }

    public int GetHashCode(GenericParameterTypeAnalysisContext type)
    {
        return HashCode.Combine(type.Type, type); // Generic parameters have unique instances, so we can use the instance itself as the hash code.
    }

    public int GetHashCode(SentinelTypeAnalysisContext type)
    {
        return HashCode.Combine(type.Type);
    }

    public int GetHashCode(CustomModifierTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        hash.Add(type.ModifierType, this);
        return hash.ToHashCode();
    }

    public int GetHashCode(PointerTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        return hash.ToHashCode();
    }

    public int GetHashCode(ByRefTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        return hash.ToHashCode();
    }

    public int GetHashCode(BoxedTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        return hash.ToHashCode();
    }

    public int GetHashCode(SzArrayTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        return hash.ToHashCode();
    }

    public int GetHashCode(ArrayTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        hash.Add(type.Rank);
        return hash.ToHashCode();
    }

    public int GetHashCode(PinnedTypeAnalysisContext type)
    {
        HashCode hash = new HashCode();
        hash.Add(type.Type);
        hash.Add(type.ElementType, this);
        return hash.ToHashCode();
    }

    public bool Equals(TypeAnalysisContext? x, TypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;

        return x.Type switch
        {
            Il2CppTypeEnum.IL2CPP_TYPE_ARRAY => Equals(x as ArrayTypeAnalysisContext, y as ArrayTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY => Equals(x as SzArrayTypeAnalysisContext, y as SzArrayTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST => Equals(x as GenericInstanceTypeAnalysisContext, y as GenericInstanceTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_VAR or Il2CppTypeEnum.IL2CPP_TYPE_MVAR => Equals(x as GenericParameterTypeAnalysisContext, y as GenericParameterSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_SENTINEL => Equals(x as SentinelTypeAnalysisContext, y as SentinelTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_PINNED => Equals(x as PinnedTypeAnalysisContext, y as PinnedTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_PTR => Equals(x as PointerTypeAnalysisContext, y as PointerTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_BYREF => Equals(x as ByRefTypeAnalysisContext, y as ByReferenceTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_BOXED => Equals(x as BoxedTypeAnalysisContext, y as BoxedTypeSignature),
            Il2CppTypeEnum.IL2CPP_TYPE_CMOD_OPT or Il2CppTypeEnum.IL2CPP_TYPE_CMOD_REQD => Equals(x as CustomModifierTypeAnalysisContext, y as CustomModifierTypeSignature),
            _ => SimpleTypeEquals(x, y),
        };

        bool SimpleTypeEquals(TypeAnalysisContext x, TypeSignature y)
        {
            if (x.Name != y.Name || x.Namespace != y.Namespace)
                return false;

            if (x.DeclaringType is not null)
                return Equals(x.DeclaringType, y.DeclaringType);
            else if (y.DeclaringType is not null)
                return false;

            //AssemblyDescriptor assemblyDescriptor = y.Scope as AssemblyDescriptor ?? (y.Scope as ModuleReference)?.
            return false;
        }
    }

    public bool Equals(TypeAnalysisContext? x, ITypeDescriptor? y)
    {
        return Equals(x, y?.ToTypeSignature());
    }

    public bool Equals(GenericInstanceTypeAnalysisContext? x, GenericInstanceTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        if (!Equals(x.GenericType, y.GenericType))
            return false;
        if (x.GenericArguments.Count != y.TypeArguments.Count)
            return false;
        for (var i = 0; i < x.GenericArguments.Count; i++)
        {
            if (!Equals(x.GenericArguments[i], y.TypeArguments[i]))
                return false;
        }
        return true;
    }

    public bool Equals(GenericParameterTypeAnalysisContext? x, GenericParameterSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return x.Index == y.Index && Equals(x.Type, y.ParameterType);
    }

    private static bool Equals(Il2CppTypeEnum x, GenericParameterType y) => (x, y) switch
    {
        (Il2CppTypeEnum.IL2CPP_TYPE_VAR, GenericParameterType.Type) => true,
        (Il2CppTypeEnum.IL2CPP_TYPE_MVAR, GenericParameterType.Method) => true,
        _ => false,
    };

    public bool Equals(SentinelTypeAnalysisContext? x, SentinelTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return true;
    }

    public bool Equals(CustomModifierTypeAnalysisContext? x, CustomModifierTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return (x.Required == y.IsRequired) && Equals(x.ElementType, y.BaseType) && Equals(x.ModifierType, y.ModifierType);
    }

    public bool Equals(PointerTypeAnalysisContext? x, PointerTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return Equals(x.ElementType, y.BaseType);
    }

    public bool Equals(ByRefTypeAnalysisContext? x, ByReferenceTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return Equals(x.ElementType, y.BaseType);
    }

    public bool Equals(BoxedTypeAnalysisContext? x, BoxedTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return Equals(x.ElementType, y.BaseType);
    }

    public bool Equals(SzArrayTypeAnalysisContext? x, SzArrayTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return Equals(x.ElementType, y.BaseType);
    }

    public bool Equals(ArrayTypeAnalysisContext? x, ArrayTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return x.Rank == y.Rank && Equals(x.ElementType, y.BaseType);
    }

    public bool Equals(PinnedTypeAnalysisContext? x, PinnedTypeSignature? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return Equals(x.ElementType, y.BaseType);
    }
}
