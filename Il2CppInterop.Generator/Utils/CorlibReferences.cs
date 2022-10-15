using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Utils;

internal static class CorlibReferences
{
    public static void RewriteReferenceToMscorlib(AssemblyNameReference assemblyNameReference)
    {
        assemblyNameReference.Name = "mscorlib";
        assemblyNameReference.Version = new Version(4, 0, 0, 0);
        assemblyNameReference.PublicKeyToken = new byte[] { 183, 122, 92, 86, 25, 52, 224, 137 };
        assemblyNameReference.Culture = "";
    }

    public static TypeReference ImportCorlibReference(this ModuleDefinition module, string @namespace, string type)
    {
        return module.ImportReference(typeof(string).Assembly.GetType($"{@namespace}.{type}"));
    }

    public static TypeReference Void(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(void));
    }

    public static TypeReference Bool(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(bool));
    }

    public static TypeReference IntPtr(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(IntPtr));
    }

    public static TypeReference String(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(string));
    }

    public static TypeReference SByte(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(sbyte));
    }

    public static TypeReference Byte(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(byte));
    }

    public static TypeReference Short(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(short));
    }

    public static TypeReference Int(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(int));
    }

    public static TypeReference Long(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(long));
    }

    public static TypeReference UShort(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(ushort));
    }

    public static TypeReference UInt(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(uint));
    }

    public static TypeReference ULong(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(ulong));
    }

    public static TypeReference Float(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(float));
    }

    public static TypeReference Double(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(double));
    }

    public static TypeReference Char(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(char));
    }

    public static TypeReference Type(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(Type));
    }

    public static TypeReference Object(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(object));
    }

    public static TypeReference Enum(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(Enum));
    }

    public static TypeReference ValueType(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(ValueType));
    }

    public static TypeReference Delegate(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(Delegate));
    }

    public static TypeReference MulticastDelegate(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(MulticastDelegate));
    }

    public static TypeReference DefaultMemberAttribute(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(DefaultMemberAttribute));
    }

    public static TypeReference NotSupportedException(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(NotSupportedException));
    }

    public static TypeReference FlagsAttribute(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(FlagsAttribute));
    }

    public static TypeReference ObsoleteAttribute(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(ObsoleteAttribute));
    }

    public static TypeReference Attribute(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(Attribute));
    }

    public static TypeReference RuntimeTypeHandle(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(RuntimeTypeHandle));
    }

    public static TypeReference ExtensionAttribute(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(ExtensionAttribute));
    }

    public static TypeReference ParamArrayAttribute(this ModuleDefinition module)
    {
        return module.ImportReference(typeof(ParamArrayAttribute));
    }

    public static TypeReference Action(this ModuleDefinition module, int n = 0)
    {
        return n switch
        {
            0 => module.ImportCorlibReference("System", "Action"),
            1 => module.ImportCorlibReference("System", "Action`1"),
            _ => module.ImportCorlibReference("System", $"Action`{n}")
        };
    }

    public static TypeReference Func(this ModuleDefinition module, int n = 0)
    {
        return n switch
        {
            0 => module.ImportCorlibReference("System", "Func`1"),
            1 => module.ImportCorlibReference("System", "Func`2"),
            _ => module.ImportCorlibReference("System", $"Func`{n}")
        };
    }

    public static MethodReference TypeGetTypeFromHandle(this ModuleDefinition module)
    {
        var type = module.Type();
        var mr = new MethodReference("GetTypeFromHandle", type, type)
        {
            HasThis = false
        };
        mr.Parameters.Add(new ParameterDefinition(module.RuntimeTypeHandle()));
        return mr;
    }

    public static MethodReference TypeGetIsValueType(this ModuleDefinition module)
    {
        var type = module.Type();
        var mr = new MethodReference("get_IsValueType", module.Bool(), type)
        {
            HasThis = true
        };
        return mr;
    }

    public static MethodReference TypeGetFullName(this ModuleDefinition module)
    {
        var type = module.Type();
        var mr = new MethodReference("get_FullName", module.String(), type)
        {
            HasThis = true
        };
        return mr;
    }

    public static MethodReference StringEquals(this ModuleDefinition module)
    {
        var mr = new MethodReference("Equals", module.Bool(), module.String())
        {
            HasThis = false
        };
        mr.Parameters.Add(new ParameterDefinition(module.String()));
        mr.Parameters.Add(new ParameterDefinition(module.String()));
        return mr;
    }

    public static MethodReference ExtensionAttributeCtor(this ModuleDefinition module)
    {
        return new MethodReference(".ctor", module.Void(), module.ExtensionAttribute()) { HasThis = true };
    }

    public static MethodReference ParamArrayAttributeCtor(this ModuleDefinition module)
    {
        return new MethodReference(".ctor", module.Void(), module.ParamArrayAttribute()) { HasThis = true };
    }

    public static MethodReference FlagsAttributeCtor(this ModuleDefinition module)
    {
        return new MethodReference(".ctor", module.Void(), module.FlagsAttribute()) { HasThis = true };
    }

    public static MethodReference NotSupportedExceptionCtor(this ModuleDefinition module)
    {
        return new MethodReference(".ctor", module.Void(), module.NotSupportedException())
        { HasThis = true, Parameters = { new ParameterDefinition(module.String()) } };
    }

    public static MethodReference ObsoleteAttributeCtor(this ModuleDefinition module)
    {
        return new MethodReference(".ctor", module.Void(), module.ObsoleteAttribute())
        { HasThis = true, Parameters = { new ParameterDefinition(module.String()) } };
    }
}
