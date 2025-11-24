using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class InvalidFieldRemovalProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Invalid Field Removal";
    public override string Id => "invalid_field_removal";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // C# allows developers to define explicit layout structs with reference type fields, but these are invalid in .NET when the field overlaps with another field.
        // This processing layer handles the most common case of this - when there are multiple fields at the same offset, and at least one of them is a reference type.

        Dictionary<int, int> offsetsToFieldCount = new();
        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;
            foreach (var type in assembly.Types)
            {
                if (type.IsInjected || !type.IsValueType || (type.Attributes & TypeAttributes.ExplicitLayout) == 0)
                    continue;

                offsetsToFieldCount.Clear();
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic)
                        continue;
                    var offset = field.Offset;
                    offsetsToFieldCount[offset] = offsetsToFieldCount.GetValueOrDefault(offset, 0) + 1;
                }

                for (var i = type.Fields.Count - 1; i >= 0; i--)
                {
                    var field = type.Fields[i];
                    if (field.IsStatic)
                        continue;

                    var offset = field.Offset;
                    if (offsetsToFieldCount[offset] > 1 && field.FieldType is { IsValueType: false } and not PointerTypeAnalysisContext)
                    {
                        type.Fields.RemoveAt(i);
                    }
                }
            }
        }
    }
}
