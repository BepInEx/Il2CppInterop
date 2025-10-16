using AsmResolver.DotNet;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.OutputFormats;

namespace Il2CppInterop.Generator;

public class AsmResolverDllOutputFormatBinding : AsmResolverDllOutputFormatThrowNull
{
    public override string OutputFormatId => "dll_binding";

    public override string OutputFormatName => "DLL files with method bodies containing a binding for modding.";

    protected override void FillMethodBody(MethodDefinition methodDefinition, MethodAnalysisContext methodContext)
    {
        if (methodContext.TryGetExtraData(out TranslatedMethodBody? translatedBody))
        {
            translatedBody.FillMethodBody(methodDefinition);
            methodContext.RemoveExtraData<TranslatedMethodBody>(); // Free up memory
        }
        else if (methodContext.TryGetExtraData(out NativeMethodBody? nativeBody))
        {
            nativeBody.FillMethodBody(methodDefinition);
            methodContext.RemoveExtraData<NativeMethodBody>(); // Free up memory
        }
        else
        {
            // This gets called when the body of an unstripped method could not be translated.
            // We could have it throw a custom exception with a message, but throwing null is sufficient for now.
            base.FillMethodBody(methodDefinition, methodContext);
        }
    }

    public override List<AssemblyDefinition> BuildAssemblies(ApplicationAnalysisContext context)
    {
        var list = base.BuildAssemblies(context);

        var referenceAssemblies = context.Assemblies.Where(a => a.IsReferenceAssembly).Select(a => a.Name).ToHashSet();

        // Remove injected reference assemblies from the output
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (referenceAssemblies.Contains(list[i].Name ?? ""))
                list.RemoveAt(i);
        }

        // Replace mscorlib references with .NET Core references
        var dotNetCorLib = KnownCorLibs.SystemRuntime_v9_0_0_0;
        foreach (var assembly in list)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var reference in module.AssemblyReferences)
                {
                    if (reference.Name == "mscorlib")
                    {
                        reference.Name = dotNetCorLib.Name;
                        reference.Version = dotNetCorLib.Version;
                        reference.Attributes = dotNetCorLib.Attributes;
                        reference.PublicKeyOrToken = dotNetCorLib.PublicKeyOrToken;
                        reference.HashValue = dotNetCorLib.HashValue;
                        reference.Culture = dotNetCorLib.Culture;
                    }
                }
            }
        }

        return list;
    }
}
