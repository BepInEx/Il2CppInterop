using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NativeBoolean : IComparable, IComparable<bool>, IEquatable<bool>, IComparable<NativeBoolean>, IEquatable<NativeBoolean>
    {
        private readonly byte Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(NativeBoolean b)
            => Unsafe.As<NativeBoolean, bool>(ref b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NativeBoolean(bool b)
            => Unsafe.As<bool, NativeBoolean>(ref b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(IFormatProvider? provider)
            => Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).ToString(provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFormat(Span<char> destination, out int charsWritten)
            => Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).TryFormat(destination, out charsWritten);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
            => obj switch
            {
                bool boolean => this == boolean,
                NativeBoolean nativeBool => this == nativeBool,
                _ => false
            };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(bool other)
            => this == other;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NativeBoolean other)
            => this == other;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(object? obj)
            => Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).CompareTo(obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(bool value)
            => Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).CompareTo(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(NativeBoolean value)
            => CompareTo(Unsafe.As<NativeBoolean, bool>(ref value));
    }
}
