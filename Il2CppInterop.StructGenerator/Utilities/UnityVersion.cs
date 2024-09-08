using System.Text.RegularExpressions;

namespace Il2CppInterop.StructGenerator.Utilities;

internal struct UnityVersion : IComparable, IComparable<UnityVersion>
{
    public int Major => unchecked((int)((myMBlob >> 48) & 0xFFFFUL));
    public int Minor => unchecked((int)((myMBlob >> 40) & 0xFFUL));
    public int Build => unchecked((int)((myMBlob >> 32) & 0xFFUL));
    public char Type => (char)unchecked((int)((myMBlob >> 24) & 0xFFUL));
    public int Revision => unchecked((int)((myMBlob >> 16) & 0xFFUL));

    public UnityVersion(string versionString)
    {
        var match = Regex.Match(versionString, @"([0-9]+).([0-9]+).([0-9]+)(?:([abcfpx])([0-9]+))?");
        if (!match.Success) throw new Exception("invalid unity version string");
        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var build = int.Parse(match.Groups[3].Value);
        var type = match.Groups[4].Success ? match.Groups[4].Value[0] : 'f';
        var revision = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 1;
        myMBlob = MakeBlob(major, minor, build, type, revision);
    }

    private static ulong MakeBlob(int major, int minor, int build, char type, int revision)
    {
        var blob = ((ulong)(major & 0xFFFF) << 48) | ((ulong)(minor & 0xFF) << 40) | ((ulong)(build & 0xFF) << 32)
                   | ((ulong)(type & 0xFF) << 24) | ((ulong)(revision & 0xFF) << 16);
        return blob;
    }

    public int CompareTo(object? obj)
    {
        if (obj is UnityVersion version)
            return CompareTo(version);
        return 1;
    }

    public int CompareTo(UnityVersion other)
    {
        if (this > other)
            return 1;
        if (this < other)
            return -1;
        return 0;
    }

    public static bool operator !=(UnityVersion lhs, UnityVersion rhs)
    {
        return lhs.myMBlob != rhs.myMBlob;
    }

    public static bool operator ==(UnityVersion lhs, UnityVersion rhs)
    {
        return lhs.myMBlob == rhs.myMBlob;
    }

    public static bool operator <(UnityVersion lhs, UnityVersion rhs)
    {
        return lhs.myMBlob < rhs.myMBlob;
    }

    public static bool operator <=(UnityVersion lhs, UnityVersion rhs)
    {
        return lhs.myMBlob <= rhs.myMBlob;
    }

    public static bool operator >(UnityVersion lhs, UnityVersion rhs)
    {
        return lhs.myMBlob > rhs.myMBlob;
    }

    public static bool operator >=(UnityVersion lhs, UnityVersion rhs)
    {
        return lhs.myMBlob >= rhs.myMBlob;
    }

    public string ToStringShort()
    {
        return $"{Major}.{Minor}.{Build}";
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Build}{Type}{Revision}";
    }

    private readonly ulong myMBlob;

    public override readonly bool Equals(object obj)
    {
        return obj is UnityVersion version && this == version;
    }

    public override readonly int GetHashCode() => myMBlob.GetHashCode();
}
