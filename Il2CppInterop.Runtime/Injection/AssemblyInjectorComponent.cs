using System;
using System.Collections.Generic;
using Il2CppInterop.Common.Host;
using Il2CppInterop.Runtime.Injection;

namespace Il2CppInterop.Runtime
{
    internal static class AssemblyListComponentExtensions
    {
        public static T AddAssemblyInjector<T, TProvider>(this T host)
            where T : BaseHost
            where TProvider : IAssemblyListProvider, new()
        {
            host.AddComponent(new AssemblyInjectorComponent(new TProvider()));
            return host;
        }
    }

    public interface IAssemblyListProvider
    {
        public IEnumerable<string> GetAssemblyList();
    }

    public class AssemblyInjectorComponent : IHostComponent
    {
        private static IAssemblyListProvider s_assemblyListProvider;

        public AssemblyInjectorComponent(IAssemblyListProvider assemblyListProvider) => s_assemblyListProvider = assemblyListProvider;

        public static IEnumerable<string> ModAssemblies
        {
            get
            {
                if (s_assemblyListProvider == null)
                {
                    throw new InvalidOperationException("Mod Assembly Injector is not initialized! Initialize the host before using Mod Assembly Injector!");
                }

                return s_assemblyListProvider.GetAssemblyList();
            }
        }

        public void Dispose()
        {
            s_assemblyListProvider = null;
        }

        public void Start()
        {
            InjectorHelpers.EarlySetup();
        }
    }
}
