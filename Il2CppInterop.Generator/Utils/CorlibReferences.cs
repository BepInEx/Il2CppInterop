using System.Reflection;
using System.Runtime.CompilerServices;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.Utils;

internal static class CorlibReferences
{
    /// <summary>
    /// This is used in the TargetFrameworkAttribute.
    /// </summary>
    public static string TargetFrameworkName => ".NET 6.0";
    public static AssemblyReference TargetCorlib => KnownCorLibs.SystemRuntime_v6_0_0_0;

    public static void RewriteCorlibReference(AssemblyReference assemblyNameReference)
    {
        CopyValues(assemblyNameReference, TargetCorlib);
    }

    private static void CopyValues(AssemblyReference target, AssemblyReference source)
    {
        target.Attributes = source.Attributes;
        target.Culture = source.Culture;
        target.DisableJitCompileOptimizer = source.DisableJitCompileOptimizer;
        target.EnableJitCompileTracking = source.EnableJitCompileTracking;
        target.HashValue = source.HashValue?.ToArray();
        target.HasPublicKey = source.HasPublicKey;
        target.IsRetargetable = source.IsRetargetable;
        target.IsWindowsRuntime = source.IsWindowsRuntime;
        target.Name = source.Name;
        target.PublicKeyOrToken = source.PublicKeyOrToken?.ToArray();
        target.Version = source.Version;
    }

    public static TypeSignature ImportCorlibReference(this ModuleDefinition module, string fullName)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(string).Assembly.GetType(fullName));
    }

    public static TypeSignature Void(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Void;
    }

    public static TypeSignature Bool(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Boolean;
    }

    public static TypeSignature IntPtr(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.IntPtr;
    }

    public static TypeSignature String(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.String;
    }

    public static TypeSignature SByte(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.SByte;
    }

    public static TypeSignature Byte(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Byte;
    }

    public static TypeSignature Short(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Int16;
    }

    public static TypeSignature Int(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Int32;
    }

    public static TypeSignature Long(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Int64;
    }

    public static TypeSignature UShort(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.UInt16;
    }

    public static TypeSignature UInt(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.UInt32;
    }

    public static TypeSignature ULong(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.UInt64;
    }

    public static TypeSignature Float(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Single;
    }

    public static TypeSignature Double(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Double;
    }

    public static TypeSignature Char(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Char;
    }

    public static TypeSignature Type(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(Type));
    }

    public static TypeSignature Object(this ModuleDefinition module)
    {
        return module.CorLibTypeFactory.Object;
    }

    public static TypeSignature Enum(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(Enum));
    }

    public static TypeSignature ValueType(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(ValueType));
    }

    public static TypeSignature Delegate(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(Delegate));
    }

    public static TypeSignature MulticastDelegate(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(MulticastDelegate));
    }

    public static TypeSignature DefaultMemberAttribute(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(DefaultMemberAttribute));
    }

    public static TypeSignature NotSupportedException(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(NotSupportedException));
    }

    public static TypeSignature FlagsAttribute(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(FlagsAttribute));
    }

    public static TypeSignature ObsoleteAttribute(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(ObsoleteAttribute));
    }

    public static TypeSignature Attribute(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(Attribute));
    }

    public static TypeSignature RuntimeTypeHandle(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(RuntimeTypeHandle));
    }

    public static TypeSignature ExtensionAttribute(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(ExtensionAttribute));
    }

    public static TypeSignature ParamArrayAttribute(this ModuleDefinition module)
    {
        return module.DefaultImporter.ImportTypeSignature(typeof(ParamArrayAttribute));
    }

    public static TypeSignature Action(this ModuleDefinition module, int n = 0)
    {
        return n switch
        {
            0 => module.ImportCorlibReference("System.Action"),
            1 => module.ImportCorlibReference("System.Action`1"),
            _ => module.ImportCorlibReference($"System.Action`{n}")
        };
    }

    public static TypeSignature Func(this ModuleDefinition module, int n = 0)
    {
        return n switch
        {
            0 => module.ImportCorlibReference("System.Func`1"),
            1 => module.ImportCorlibReference("System.Func`2"),
            _ => module.ImportCorlibReference($"System.Func`{n + 1}")
        };
    }

    public static MemberReference TypeGetTypeFromHandle(this ModuleDefinition module)
    {
        var type = module.Type();
        MethodSignature signature = MethodSignature.CreateStatic(type, module.RuntimeTypeHandle());
        return new MemberReference(type.ToTypeDefOrRef(), nameof(System.Type.GetTypeFromHandle), signature);
    }

    public static MemberReference TypeGetIsValueType(this ModuleDefinition module)
    {
        var type = module.Type();
        return new MemberReference(type.ToTypeDefOrRef(), "get_IsValueType", MethodSignature.CreateInstance(module.Bool()));
    }

    public static MemberReference TypeGetFullName(this ModuleDefinition module)
    {
        var type = module.Type();
        return new MemberReference(type.ToTypeDefOrRef(), "get_FullName", MethodSignature.CreateInstance(module.String()));
    }

    public static MemberReference StringEquals(this ModuleDefinition module)
    {
        var @string = module.String();
        MethodSignature signature = MethodSignature.CreateStatic(module.Bool(), @string, @string);
        return new MemberReference(@string.ToTypeDefOrRef(), nameof(string.Equals), signature);
    }

    public static MemberReference ExtensionAttributeCtor(this ModuleDefinition module)
    {
        return MakeConstructorReference(module, module.ExtensionAttribute().ToTypeDefOrRef());
    }

    public static MemberReference ParamArrayAttributeCtor(this ModuleDefinition module)
    {
        return MakeConstructorReference(module, module.ParamArrayAttribute().ToTypeDefOrRef());
    }

    public static MemberReference FlagsAttributeCtor(this ModuleDefinition module)
    {
        return MakeConstructorReference(module, module.FlagsAttribute().ToTypeDefOrRef());
    }

    public static MemberReference NotSupportedExceptionCtor(this ModuleDefinition module)
    {
        return MakeConstructorReference(module, module.NotSupportedException().ToTypeDefOrRef(), module.String());
    }

    public static MemberReference ObsoleteAttributeCtor(this ModuleDefinition module)
    {
        return MakeConstructorReference(module, module.ObsoleteAttribute().ToTypeDefOrRef(), module.String());
    }

    private static MemberReference MakeConstructorReference(ModuleDefinition module, ITypeDefOrRef type, params TypeSignature[] parameters)
    {
        MethodSignature signature = MethodSignature.CreateInstance(module.Void(), parameters);
        return new MemberReference(type, ".ctor", signature);
    }
}
