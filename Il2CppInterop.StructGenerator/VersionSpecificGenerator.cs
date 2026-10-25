using System.CodeDom.Compiler;
using AssetRipper.Primitives;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator;

internal abstract class VersionSpecificGenerator
{
    public VersionSpecificGenerator(string metadataSuffix, CppClass nativeClass)
    {
        MetadataSuffix = metadataSuffix;
        NativeStructGenerator = new NativeStructGenerator(MetadataSuffix, nativeClass);

        HandlerGenerator = new StructHandlerGenerator(HandlerName, HandlerInterface,
            NativeInterface, NativeStub, NativeStructGenerator, CreateNewParameters, CreateNewExtraBody)
        {
            SizeProviderOverride = SizeOverride
        };
        HandlerGenerator.HandlerClass.NestedElements.Add(NativeStructGenerator.NativeStruct);

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

    public string Namespace => $"Il2CppInterop.Runtime.Structs.VersionSpecific.{GeneratorName}";
    public abstract string GeneratorName { get; }
    public string HandlerName => $"Native{GeneratorName}StructHandler_{MetadataSuffix}";
    public string HandlerInterface => $"INative{GeneratorName}StructHandler";
    public string NativeInterface => $"INative{GeneratorName}Struct";
    protected virtual string NativeStub => $"Il2Cpp{GeneratorName}";

    protected virtual IEnumerable<CodeGenParameter>? CreateNewParameters => null;
    protected virtual string? SizeOverride => null;

    protected virtual IEnumerable<CodeGenField>? WrapperFields => null;
    protected virtual IEnumerable<CodeGenProperty>? WrapperProperties => null;
    protected virtual IEnumerable<ByRefWrapper>? ByRefWrappers => null;
    protected virtual IEnumerable<BitfieldAccessor>? BitfieldAccessors => null;

    protected virtual Action<IndentedTextWriter>? CreateNewExtraBody => null;

    public string MetadataSuffix { get; }
    public NativeStructGenerator NativeStructGenerator { get; }
    public StructHandlerGenerator HandlerGenerator { get; }
    public StructWrapperGenerator WrapperGenerator { get; }
    public HashSet<UnityVersion> ApplicableVersions { get; } = [];
    public HashSet<string> ExtraUsings { get; } = [];

    public void SetupElements()
    {
        List<CodeGenProperty> properties =
        [
            new CodeGenProperty($"{NativeStructGenerator.NativeStruct.Name}*", ElementProtection.Private, "_")
            { ImmediateGet = $"({NativeStructGenerator.NativeStruct.Name}*)Pointer" }
        ];
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
                            property.ImmediateGet = "throw new System.NotSupportedException()";
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

    public CodeGenFile GenerateInterfacesFile()
    {
        var createNewStructMethod = new CodeGenMethod(NativeInterface, null, "CreateNewStruct")
        {
            HasBody = false
        };
        createNewStructMethod.Parameters.AddRange(CreateNewParameters ?? []);
        var handlerInterface = new CodeGenInterface(ElementProtection.Public, HandlerInterface)
        {
            InterfaceNames = { "INativeStructHandler" },
            Methods =
            {
                createNewStructMethod,
                new CodeGenMethod(NativeInterface, null, "Wrap")
                {
                    IsUnsafe = true,
                    HasBody = false,
                    Parameters =
                    {
                        new CodeGenParameter($"{NativeStub}*", "pointer"),
                    }
                },
            }
        };
        var nativeInterface = new CodeGenInterface(ElementProtection.Public, NativeInterface)
        {
            InterfaceNames = { "INativeStruct" },
        };
        foreach (var property in WrapperProperties ?? [])
        {
            if (property.Protection != ElementProtection.Public)
                continue;

            nativeInterface.Properties.Add(new CodeGenProperty(property.Type, null, property.Name)
            {
                EmptyGet = property.HasGet,
                EmptySet = property.HasSet,
                IsUnsafe = property.IsUnsafe || property.Type.Contains('*'),
            });
        }
        foreach (var wrapper in ByRefWrappers ?? [])
        {
            nativeInterface.Properties.Add(new CodeGenProperty($"ref {wrapper.WrappedType}", null, wrapper.WrappedName)
            {
                IsUnsafe = wrapper.WrappedType.Contains('*'),
                EmptyGet = true,
            });
        }
        foreach (var accessor in BitfieldAccessors ?? [])
        {
            nativeInterface.Properties.Add(new CodeGenProperty(accessor.AccessorType, null, accessor.AccessorName)
            {
                EmptyGet = true,
                EmptySet = true,
            });
        }
        var file = new CodeGenFile()
        {
            Namespace = Namespace,
            Elements =
            {
                handlerInterface,
                nativeInterface,
            }
        };
        file.Usings.AddRange(ExtraUsings);
        return file;
    }
}
