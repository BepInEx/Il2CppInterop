using System.Text;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;
using Il2CppInterop.StructGenerator.Utilities;

namespace Il2CppInterop.StructGenerator;

internal class BitfieldAccessor
{
    public BitfieldAccessor(string accessorName, string elementName, string accessorType = "bool",
        bool generateIfNotPresent = true, string? defaultGetter = "", Action<StringBuilder>? defaultGetBuilder = null,
        Action<StringBuilder>? defaultSetBuilder = null)
    {
        AccessorName = accessorName;
        ElementName = elementName;
        AccessorType = accessorType;
        GenerateIfNotPresent = generateIfNotPresent;
        DefaultImmediateGetter = defaultGetter;
        DefaultGetBuilder = defaultGetBuilder;
        DefaultSetBuilder = defaultSetBuilder;
    }

    public string AccessorName { get; }
    public string ElementName { get; }
    public string AccessorType { get; }
    public bool GenerateIfNotPresent { get; }
    public string? DefaultImmediateGetter { get; }
    public Action<StringBuilder>? DefaultGetBuilder { get; }
    public Action<StringBuilder>? DefaultSetBuilder { get; }
}

internal abstract class VersionSpecificGenerator
{
    public VersionSpecificGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null)
    {
        MetadataSuffix = metadataSuffix;
        NativeStructGenerator = new NativeStructGenerator(MetadataSuffix, nativeClass);

        HandlerGenerator = new StructHandlerGenerator($"{HandlerName}_{MetadataSuffix}", HandlerInterface,
            NativeInterface, NativeStub, NativeStructGenerator, CreateNewParameters, CreateNewExtraBody)
        {
            SizeProviderOverride = SizeOverride
        };
        HandlerGenerator.HandlerClass.NestedElements.Add(NativeStructGenerator.NativeStruct);
        if (DependsOnClasses != null && dependencyResolver != null)
            foreach (var dependency in DependsOnClasses)
                HandlerGenerator.HandlerClass.NestedElements.Add(
                    new NativeStructGenerator(MetadataSuffix, dependencyResolver(dependency)).NativeStruct);

        WrapperGenerator = new StructWrapperGenerator(NativeInterface);
        foreach (var bitfieldField in NativeStructGenerator.NativeStruct.Fields.Where(x =>
                     x.Name.StartsWith("_bitfield")))
            WrapperGenerator.WrapperClass.Fields.Add(
                new CodeGenField("int", ElementProtection.Private, $"{bitfieldField.Name}offset")
                {
                    IsStatic = true,
                    DefaultValue =
                        $"Marshal.OffsetOf<{NativeStructGenerator.NativeStruct.Name}>(nameof({NativeStructGenerator.NativeStruct.Name}.{bitfieldField.Name})).ToInt32()"
                });
        HandlerGenerator.HandlerClass.NestedElements.Add(WrapperGenerator.WrapperClass);
    }

    protected abstract string HandlerName { get; }
    protected abstract string HandlerInterface { get; }
    protected abstract string NativeInterface { get; }
    protected abstract string NativeStub { get; }

    protected virtual string[]? DependsOnClasses { get; }
    protected virtual List<CodeGenParameter>? CreateNewParameters => null;
    protected virtual string? SizeOverride => null;

    protected abstract List<CodeGenField>? WrapperFields { get; }
    protected abstract List<CodeGenProperty>? WrapperProperties { get; }
    protected abstract List<ByRefWrapper>? ByRefWrappers { get; }
    protected abstract List<BitfieldAccessor>? BitfieldAccessors { get; }

    protected virtual Action<StringBuilder>? CreateNewExtraBody => null;

    public string MetadataSuffix { get; }
    public NativeStructGenerator NativeStructGenerator { get; }
    public StructHandlerGenerator HandlerGenerator { get; }
    public StructWrapperGenerator WrapperGenerator { get; }
    public HashSet<UnityVersion> ApplicableVersions { get; } = new();
    public HashSet<string> ExtraUsings { get; } = new();

    public void AddExtraUsing(string @using)
    {
        if (ExtraUsings.Contains(@using)) return;
        ExtraUsings.Add(@using);
    }

    public void SetupElements()
    {
        List<CodeGenProperty> properties = new()
        {
            new CodeGenProperty($"{NativeStructGenerator.NativeStruct.Name}*", ElementProtection.Private, "_")
            { ImmediateGet = $"({NativeStructGenerator.NativeStruct.Name}*)Pointer" }
        };
        var wrapperProperties = WrapperProperties;
        if (wrapperProperties != null) properties.AddRange(wrapperProperties);
        WrapperGenerator.ImplementProperties(properties);

        var byrefWrappers = ByRefWrappers;
        if (byrefWrappers != null)
            foreach (var wrapper in byrefWrappers)
            {
                CodeGenProperty property = new($"ref {wrapper.WrappedType}", ElementProtection.Public,
                    wrapper.WrappedName);
                string? nativeName = null;
                var nativeType = wrapper.ForcedNativeType;
                if (nativeType == null)
                {
                    foreach (var name in wrapper.NativeNames)
                    {
                        var nativeField =
                            NativeStructGenerator.NativeStruct.Fields.SingleOrDefault(x => x.Name == name);
                        if (nativeField is null) continue;
                        nativeName = nativeField.Name;
                        nativeType = nativeField.Type;
                        break;
                    }

                    if (nativeName == null || nativeType == null)
                    {
                        if (wrapper.AddNotSupported)
                        {
                            property.ImmediateGet = "throw new NotSupportedException()";
                            WrapperGenerator.WrapperClass.Properties.Add(property);
                        }

                        if (wrapper.MakeDummyIfNotSupported)
                        {
                            if (nativeName == null) nativeName = wrapper.NativeNames.First();
                            property.ImmediateGet = $"ref _{nativeName}Dummy";
                            WrapperGenerator.WrapperClass.Fields.Add(new CodeGenField($"{wrapper.WrappedType}",
                                ElementProtection.Private, $"_{nativeName}Dummy"));
                            WrapperGenerator.WrapperClass.Properties.Add(property);
                        }

                        continue;
                    }
                }

                property.ImmediateGet = "ref ";
                if (wrapper.WrappedType != nativeType)
                    property.ImmediateGet += $"*({wrapper.WrappedType}*)&";
                property.ImmediateGet += $"_->{nativeName}";

                WrapperGenerator.WrapperClass.Properties.Add(property);
            }

        var bitfieldAccessors = BitfieldAccessors;
        if (bitfieldAccessors != null)
            foreach (var bitfieldAccessor in bitfieldAccessors)
            {
                var bitfieldType = "";
                foreach (var nestedElement in NativeStructGenerator.NativeStruct.NestedElements)
                {
                    if (nestedElement is not CodeGenEnum enumElement) continue;
                    if (!enumElement.Elements.Any(x => x.Name == bitfieldAccessor.ElementName)) continue;
                    bitfieldType = enumElement.Name;
                    break;
                }

                if (bitfieldType == "")
                {
                    if (bitfieldAccessor.GenerateIfNotPresent)
                    {
                        CodeGenProperty defaultProperty = new(bitfieldAccessor.AccessorType, ElementProtection.Public,
                            bitfieldAccessor.AccessorName);
                        if (bitfieldAccessor.DefaultGetBuilder != null)
                        {
                            defaultProperty.GetMethod ??= new CodeGenMethod(bitfieldAccessor.AccessorType,
                                ElementProtection.Private, "get");
                            defaultProperty.GetMethod.MethodBodyBuilder = bitfieldAccessor.DefaultGetBuilder;
                        }

                        if (bitfieldAccessor.DefaultSetBuilder != null)
                        {
                            defaultProperty.SetMethod ??= new CodeGenMethod(bitfieldAccessor.AccessorType,
                                ElementProtection.Private, "set");
                            defaultProperty.SetMethod.MethodBodyBuilder = bitfieldAccessor.DefaultSetBuilder;
                        }

                        if (bitfieldAccessor.DefaultImmediateGetter != null)
                        {
                            defaultProperty.SetMethod ??= new CodeGenMethod(bitfieldAccessor.AccessorType,
                                ElementProtection.Private, "set");
                            defaultProperty.SetMethod.ImmediateReturn = "";
                            defaultProperty.GetMethod ??= new CodeGenMethod(bitfieldAccessor.AccessorType,
                                ElementProtection.Private, "get");
                            defaultProperty.GetMethod.ImmediateReturn = bitfieldAccessor.DefaultImmediateGetter;
                        }

                        WrapperGenerator.WrapperClass.Properties.Add(defaultProperty);
                    }

                    continue;
                }

                WrapperGenerator.WrapperClass.Properties.Add(
                    new CodeGenProperty(bitfieldAccessor.AccessorType, ElementProtection.Public,
                        bitfieldAccessor.AccessorName)
                    {
                        GetMethod = new CodeGenMethod(bitfieldAccessor.AccessorType, ElementProtection.Private, "get")
                        {
                            ImmediateReturn =
                                $"this.CheckBit(_{bitfieldType.ToLower()}offset, (int){NativeStructGenerator.NativeStruct.Name}.{bitfieldType}.BIT_{bitfieldAccessor.ElementName})"
                        },
                        SetMethod = new CodeGenMethod(bitfieldAccessor.AccessorType, ElementProtection.Private, "set")
                        {
                            ImmediateReturn =
                                $"this.SetBit(_{bitfieldType.ToLower()}offset, (int){NativeStructGenerator.NativeStruct.Name}.{bitfieldType}.BIT_{bitfieldAccessor.ElementName}, value)"
                        }
                    });
            }

        var wrapperFields = WrapperFields;
        if (wrapperFields != null)
            WrapperGenerator.WrapperClass.Fields.AddRange(wrapperFields);
    }

    protected CodeGenField? GetNativeField(string name)
    {
        return NativeStructGenerator.NativeStruct.Fields.SingleOrDefault(x => x.Name == name);
    }

    public virtual string Build()
    {
        return HandlerGenerator.HandlerClass.Build();
    }
}
