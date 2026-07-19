using Microsoft.CodeAnalysis;

namespace Il2CppInterop.SourceGenerator;

internal readonly record struct MemberModel(
    string Name,
    string Type,
    MemberKind Kind,
    int Index,
    Accessibility? Accessibility,
    bool IsStatic);
