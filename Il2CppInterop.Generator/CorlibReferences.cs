using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Il2CppInterop.Generator.MetadataAccess;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Il2CppInterop.Generator
{
    public static class CorlibReferences
    {
        internal enum ElementType : byte
        {
            Void = 1,
            Boolean = 2,
            I4 = 8,
            I8 = 10,
            String = 14,
            I = 24,
            Object = 28,
        }

        private static Action<TypeReference, ElementType> setEType;

        static CorlibReferences()
        {
            var dm = new DynamicMethod(nameof(setEType), typeof(void), new[] { typeof(TypeReference), typeof(ElementType) },
                typeof(CorlibReferences), true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, typeof(TypeReference).GetField("etype", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            setEType = (Action<TypeReference, ElementType>)dm.CreateDelegate(typeof(Action<TypeReference, ElementType>));
        }

        private static AssemblyNameReference GetCoreLibraryReference(ModuleDefinition module)
        {
            var corlib = module.AssemblyReferences.FirstOrDefault(ar => ar.Name is "mscorlib" or "netstandard" or "System.Runtime" or "System.Private.CoreLib");
            if (corlib is not null)
                return corlib;
            corlib = new AssemblyNameReference("mscorlib", new Version(4, 0, 0, 0))
            {
                PublicKeyToken = new byte[] { 183, 122, 92, 86, 25, 52, 224, 137 }
            };
            module.AssemblyReferences.Add(corlib);
            return corlib;
        }

        public static TypeReference ImportCorlibReference(this ModuleDefinition module, string @namespace, string @type)
        {
            return new(@namespace, @type, module, GetCoreLibraryReference(module));
        }

        public static TypeReference ImportCorlibReference(this ModuleDefinition module, string @namespace, string @type, string[] genericParams)
        {
            var typeRef = module.ImportCorlibReference(@namespace, @type);
            foreach (var genericParam in genericParams)
                typeRef.GenericParameters.Add(new(genericParam, typeRef));
            return typeRef;
        }


        public static TypeReference Void(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "Void", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.Void);
            return res;
        }

        public static TypeReference Bool(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "Boolean", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.Boolean);
            res.IsValueType = true;
            return res;
        }

        public static TypeReference IntPtr(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "IntPtr", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.I);
            res.IsValueType = true;
            return res;
        }

        public static TypeReference String(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "String", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.String);
            return res;
        }

        public static TypeReference Int(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "Int32", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.I4);
            res.IsValueType = true;
            return res;
        }

        public static TypeReference Long(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "Int64", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.I8);
            res.IsValueType = true;
            return res;
        }

        public static TypeReference Type(this ModuleDefinition module) => new("System", "Type", module, GetCoreLibraryReference(module));

        public static TypeReference Object(this ModuleDefinition module)
        {
            var res = new TypeReference("System", "Object", module, GetCoreLibraryReference(module));
            setEType(res, ElementType.Object);
            return res;
        }

        public static TypeReference Enum(this ModuleDefinition module) => new("System", "Enum", module, GetCoreLibraryReference(module));

        public static TypeReference ValueType(this ModuleDefinition module) => new("System", "ValueType", module, GetCoreLibraryReference(module));

        public static TypeReference Delegate(this ModuleDefinition module) => new("System", "Delegate", module, GetCoreLibraryReference(module));

        public static TypeReference MulticastDelegate(this ModuleDefinition module) => new("System", "MulticastDelegate", module, GetCoreLibraryReference(module));

        public static TypeReference DefaultMemberAttribute(this ModuleDefinition module) => new("System.Reflection", "DefaultMemberAttribute", module, GetCoreLibraryReference(module));

        public static TypeReference NotSupportedException(this ModuleDefinition module) => new("System", "NotSupportedException", module, GetCoreLibraryReference(module));

        public static TypeReference FlagsAttribute(this ModuleDefinition module) => new("System", "FlagsAttribute", module, GetCoreLibraryReference(module));

        public static TypeReference ObsoleteAttribute(this ModuleDefinition module) => new("System", "ObsoleteAttribute", module, GetCoreLibraryReference(module));

        public static TypeReference Attribute(this ModuleDefinition module) => new("System", "Attribute", module, GetCoreLibraryReference(module));

        public static TypeReference RuntimeTypeHandle(this ModuleDefinition module) => new("System", "RuntimeTypeHandle", module, GetCoreLibraryReference(module));

        public static TypeReference ExtensionAttribute(this ModuleDefinition module) => new("System.Runtime.CompilerServices", "ExtensionAttribute", module, GetCoreLibraryReference(module));

        public static TypeReference ParamArrayAttribute(this ModuleDefinition module) => new("System", "ParamArrayAttribute", module, GetCoreLibraryReference(module));

        public static TypeReference Action(this ModuleDefinition module, int n = 0)
        {
            return n switch
            {
                0 => module.ImportCorlibReference("System", "Action"),
                1 => module.ImportCorlibReference("System", $"Action`1", new string[] { "T" }),
                _ => module.ImportCorlibReference("System", $"Action`{n}",
                    Enumerable.Range(1, n).Select(i => $"T{i}").ToArray())
            };
        }

        public static TypeReference Func(this ModuleDefinition module, int n = 0)
        {
            return n switch
            {
                0 => module.ImportCorlibReference("System", "Func`1", new string[] { "TResult" }),
                1 => module.ImportCorlibReference("System", $"Func`2", new string[] { "T", "TResult" }),
                _ => module.ImportCorlibReference("System", $"Func`{n}",
                    Enumerable.Range(1, n).Select(i => $"T{i}").Concat(new string[] { "TResult" }).ToArray())
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
            return new MethodReference(".ctor", module.Void(), module.NotSupportedException()) { HasThis = true, Parameters = { new ParameterDefinition(module.String()) } };
        }

        public static MethodReference ObsoleteAttributeCtor(this ModuleDefinition module)
        {
            return new MethodReference(".ctor", module.Void(), module.ObsoleteAttribute()) { HasThis = true, Parameters = { new ParameterDefinition(module.String()) } };
        }
    }
}