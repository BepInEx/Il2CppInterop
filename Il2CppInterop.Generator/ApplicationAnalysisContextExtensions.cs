using System.Reflection;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class ApplicationAnalysisContextExtensions
{
    extension (ApplicationAnalysisContext appContext)
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
    }
}
