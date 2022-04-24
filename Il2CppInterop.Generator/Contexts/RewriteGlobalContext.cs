using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.MetadataAccess;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Contexts
{
    public class RewriteGlobalContext : IDisposable
    {
        public GeneratorOptions Options { get; }
        public IIl2CppMetadataAccess GameAssemblies { get; }
        public IMetadataAccess UnityAssemblies { get; }

        private readonly Dictionary<string, AssemblyRewriteContext> myAssemblies = new Dictionary<string, AssemblyRewriteContext>();
        private readonly Dictionary<AssemblyDefinition, AssemblyRewriteContext> myAssembliesByOld = new Dictionary<AssemblyDefinition, AssemblyRewriteContext>();

        internal readonly Dictionary<(object, string, int), List<TypeDefinition>> RenameGroups = new Dictionary<(object, string, int), List<TypeDefinition>>();
        internal readonly Dictionary<TypeDefinition, string> RenamedTypes = new Dictionary<TypeDefinition, string>();
        internal readonly Dictionary<TypeDefinition, string> PreviousRenamedTypes = new Dictionary<TypeDefinition, string>();

        internal readonly List<long> MethodStartAddresses = new List<long>();

        public IEnumerable<AssemblyRewriteContext> Assemblies => myAssemblies.Values;

        internal bool HasGcWbarrierFieldWrite { get; set; }

        public RewriteGlobalContext(GeneratorOptions options, IIl2CppMetadataAccess gameAssemblies, IMetadataAccess unityAssemblies)
        {
            Options = options;
            GameAssemblies = gameAssemblies;
            UnityAssemblies = unityAssemblies;

            foreach (var sourceAssembly in gameAssemblies.Assemblies)
            {
                var assemblyName = sourceAssembly.Name.Name;
                if (assemblyName == "Il2CppDummyDll")
                {
                    sourceAssembly.Dispose();
                    continue;
                }

                var newAssembly = AssemblyDefinition.CreateAssembly(
                    new AssemblyNameDefinition(sourceAssembly.Name.Name.UnSystemify(options), sourceAssembly.Name.Version),
                    sourceAssembly.MainModule.Name.UnSystemify(options), sourceAssembly.MainModule.Kind);

                var assemblyRewriteContext = new AssemblyRewriteContext(this, sourceAssembly, newAssembly);
                AddAssemblyContext(assemblyName, assemblyRewriteContext);
            }
        }

        internal void AddAssemblyContext(string assemblyName, AssemblyRewriteContext context)
        {
            myAssemblies[assemblyName] = context;
            if (context.OriginalAssembly != null)
                myAssembliesByOld[context.OriginalAssembly] = context;
        }

        public AssemblyRewriteContext GetNewAssemblyForOriginal(AssemblyDefinition oldAssembly)
        {
            return myAssembliesByOld[oldAssembly];
        }

        public TypeRewriteContext GetNewTypeForOriginal(TypeDefinition originalType)
        {
            return GetNewAssemblyForOriginal(originalType.Module.Assembly)
                .GetContextForOriginalType(originalType);
        }

        public TypeRewriteContext? TryGetNewTypeForOriginal(TypeDefinition originalType)
        {
            if (!myAssembliesByOld.TryGetValue(originalType.Module.Assembly, out var assembly))
                return null;
            return assembly.TryGetContextForOriginalType(originalType);
        }

        public TypeRewriteContext.TypeSpecifics JudgeSpecificsByOriginalType(TypeReference typeRef)
        {
            if (typeRef.IsPrimitive || typeRef.IsPointer || typeRef.FullName == "System.TypedReference") return TypeRewriteContext.TypeSpecifics.BlittableStruct;
            if (typeRef.FullName == "System.String" || typeRef.FullName == "System.Object" || typeRef.IsArray || typeRef.IsByReference || typeRef.IsGenericParameter || typeRef.IsGenericInstance)
                return TypeRewriteContext.TypeSpecifics.ReferenceType;

            var fieldTypeContext = GetNewTypeForOriginal(typeRef.Resolve());
            return fieldTypeContext.ComputedTypeSpecifics;
        }

        public AssemblyRewriteContext GetAssemblyByName(string name)
        {
            return myAssemblies[name];
        }

        public AssemblyRewriteContext? TryGetAssemblyByName(string name)
        {
            if (myAssemblies.TryGetValue(name, out var result))
                return result;

            if (name == "netstandard")
                return myAssemblies.TryGetValue("mscorlib", out var result2) ? result2 : null;

            return null;
        }

        public void Dispose()
        {
            var resolverCacheField = typeof(DefaultAssemblyResolver).
                GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var assembly in Assemblies)
            {
                foreach (var module in assembly.NewAssembly.Modules)
                {
                    var resolver = (DefaultAssemblyResolver)module.AssemblyResolver;
                    var cache = (Dictionary<string, AssemblyDefinition>)resolverCacheField!.GetValue(resolver);
                    cache.Clear();
                }
                assembly.NewAssembly.Dispose();
                assembly.OriginalAssembly.Dispose();
            }

            UnityAssemblies.Dispose();
        }

        public MethodDefinition? CreateParamsMethod(MethodDefinition originalMethod, MethodDefinition newMethod, AssemblyKnownImports imports, Func<TypeReference, TypeReference?> resolve)
        {
            if (newMethod.Name == "Invoke")
                return null;

            var paramsParameters = originalMethod.Parameters.Where(parameter =>
                parameter.ParameterType is ArrayType { Rank: 1 } arrayType
                && !(resolve(arrayType.ElementType)?.IsGenericParameter ?? true)
                && parameter.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == typeof(ParamArrayAttribute).FullName)
            ).ToArray();

            if (paramsParameters.Any())
            {
                var paramsMethod = new MethodDefinition(newMethod.Name, newMethod.Attributes, newMethod.ReturnType);
                foreach (var genericParameter in newMethod.GenericParameters)
                {
                    paramsMethod.GenericParameters.Add(genericParameter);
                }

                foreach (var originalParameter in originalMethod.Parameters)
                {
                    var isParams = paramsParameters.Contains(originalParameter);

                    TypeReference? convertedType;
                    if (isParams && originalParameter.ParameterType is ArrayType arrayType)
                    {
                        var resolvedElementType = resolve(arrayType.GetElementType());
                        convertedType = resolvedElementType == null ? null : new ArrayType(resolvedElementType, arrayType.Rank);
                    }
                    else
                    {
                        convertedType = resolve(originalParameter.ParameterType);
                    }

                    var parameter = new ParameterDefinition(originalParameter.Name, originalParameter.Attributes, convertedType);

                    if (isParams)
                    {
                        parameter.CustomAttributes.Add(new CustomAttribute(newMethod.Module.ParamArrayAttributeCtor()));
                    }

                    paramsMethod.Parameters.Add(parameter);
                }

                var body = paramsMethod.Body.GetILProcessor();

                if (newMethod.HasThis)
                {
                    body.Emit(OpCodes.Ldarg_0);
                }

                var argOffset = newMethod.HasThis ? 1 : 0;

                for (var i = 0; i < newMethod.Parameters.Count; i++)
                {
                    body.Emit(OpCodes.Ldarg, argOffset + i);

                    var parameter = originalMethod.Parameters[i];
                    if (paramsParameters.Contains(parameter))
                    {
                        var parameterType = (ArrayType)parameter.ParameterType;

                        MethodReference constructorReference;

                        var elementType = parameterType.GetElementType();
                        if (elementType.FullName == "System.String")
                        {
                            constructorReference = imports.Il2CppStringArrayCtor;
                        }
                        else
                        {
                            var convertedElementType = resolve(elementType)!;

                            constructorReference = imports.Module.ImportReference(convertedElementType.IsValueType ? imports.Il2CppStructArrayCtor : imports.Il2CppReferenceArrayCtor);

                            var declaringType = (GenericInstanceType)constructorReference.DeclaringType;
                            declaringType.GenericArguments.Clear();
                            declaringType.GenericArguments.Add(convertedElementType);
                        }

                        body.Emit(OpCodes.Newobj, constructorReference);
                    }
                }

                body.Emit(OpCodes.Call, newMethod);
                body.Emit(OpCodes.Ret);

                return paramsMethod;
            }

            return null;
        }
    }
}