namespace Il2CppSystem;

public interface IValueType : IObject
{
    int Size { get; }
    void WriteToSpan(System.Span<byte> span);
}
