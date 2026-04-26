using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL;

namespace Il2CppInterop.Generator.Extensions;

internal static class ApplicationAnalysisContextExtensions
{
    extension(ApplicationAnalysisContext appContext)
    {
        public InjectedAssemblyAnalysisContext InjectAssembly(Assembly assembly)
        {
            return appContext.InjectAssembly(assembly.GetName());
        }

        public InjectedAssemblyAnalysisContext InjectAssembly(AssemblyName assemblyName)
        {
#pragma warning disable SYSLIB0037 // Type or member is obsolete
            return appContext.InjectAssembly(
                assemblyName.Name!,
                assemblyName.Version,
                (uint)assemblyName.HashAlgorithm,
                (uint)assemblyName.Flags,
                assemblyName.CultureName,
                assemblyName.GetPublicKeyToken(),
                assemblyName.GetPublicKey());
#pragma warning restore SYSLIB0037 // Type or member is obsolete
        }

        public TypeAnalysisContext ResolveTypeOrThrow(Type? type)
        {
            return appContext.ResolveType(type) ?? throw new($"Unable to resolve type {type?.FullName}");
        }

        public TypeAnalysisContext? ResolveType(Type? type)
        {
            if (type is null)
                return null;

            var assemblyName = type.Assembly.GetName().Name!;
            if (assemblyName == "System.Private.CoreLib")
                assemblyName = "mscorlib";
            var assembly = appContext.GetAssemblyByName(assemblyName);
            return assembly?.GetTypeByFullName(type.FullName!);
        }

        public AssemblyAnalysisContext Mscorlib => appContext.AssembliesByName["mscorlib"];
        public AssemblyAnalysisContext Il2CppMscorlib => appContext.AssembliesByName["Il2Cppmscorlib"];

        [return: NotNullIfNotNull(nameof(methodReference))]
        public ConcreteGenericMethodAnalysisContext? ResolveContextForMethod(Cpp2IlMethodRef? methodReference)
        {
            return methodReference is not null
                ? appContext.ConcreteGenericMethodsByRef.TryGetValue(methodReference, out var context) ? context : new(methodReference, appContext)
                : null;
        }
    }
}
