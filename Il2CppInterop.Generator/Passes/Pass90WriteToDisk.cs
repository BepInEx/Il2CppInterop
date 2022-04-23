using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Generator.Contexts;
using Mono.Cecil;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator.Passes
{
    public static class Pass90WriteToDisk
    {
        public static void DoPass(RewriteGlobalContext context, GeneratorOptions options)
        {
            var registerMethod = typeof(DefaultAssemblyResolver).GetMethod("RegisterAssembly", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (AssemblyRewriteContext asmContext in context.Assemblies)
            {
                var module = asmContext.NewAssembly.MainModule;
                if (module.AssemblyResolver is DefaultAssemblyResolver resolver)
                {
                    foreach (AssemblyNameReference reference in module.AssemblyReferences)
                    {
                        var match = context.Assemblies.FirstOrDefault(f => f.NewAssembly.FullName == reference.FullName);
                        if (match != null) registerMethod!.Invoke(resolver, new object[] { match.NewAssembly });
                    }
                }
            }

            var tasks = context.Assemblies.Where(it => !options.AdditionalAssembliesBlacklist.Contains(it.NewAssembly.Name.Name)).Select(assemblyContext => Task.Run(() =>
            {
                assemblyContext.NewAssembly.Write(options.OutputDir + "/" + assemblyContext.NewAssembly.Name.Name + ".dll");
            })).ToArray();

            Task.WaitAll(tasks);
        }
    }
}