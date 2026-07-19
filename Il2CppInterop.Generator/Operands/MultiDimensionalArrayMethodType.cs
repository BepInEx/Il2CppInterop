namespace Il2CppInterop.Generator.Operands;

public enum MultiDimensionalArrayMethodType
{
    /// <summary>
    /// call instance bool bool[0..., 0...]::Get(int32, int32)
    /// </summary>
    Get,
    /// <summary>
    /// call instance void bool[0..., 0...]::Set(int32, int32, bool)
    /// </summary>
    Set,
    /// <summary>
    /// newobj instance void bool[0..., 0...]::.ctor(int32, int32)
    /// </summary>
    Constructor,
    /// <summary>
    /// call instance bool&amp; bool[0..., 0...]::Address(int32, int32)
    /// </summary>
    Address,
}
