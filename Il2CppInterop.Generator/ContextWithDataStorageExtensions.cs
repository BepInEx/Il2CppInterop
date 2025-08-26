using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class ContextWithDataStorageExtensions
{
    // Prevent boxing a large number of booleans.
    private static readonly object True = true;
    private static readonly object False = false;

    extension(ContextWithDataStorage context)
    {
        public void PutExtraData<T>(T data) where T : class
        {
            context.PutExtraData(typeof(T).Name, data);
        }

        public T? GetExtraData<T>() where T : class
        {
            return context.GetExtraData<T>(typeof(T).Name);
        }

        public bool TryGetExtraData<T>([NotNullWhen(true)] out T? data) where T : class
        {
            data = context.GetExtraData<T>();
            return data is not null;
        }

        public bool HasExtraData<T>() where T : class
        {
            return context.TryGetExtraData<T>(out _);
        }

        /// <summary>
        /// This type or member was stripped by the compiler, but we restored it using external assemblies.
        /// </summary>
        public bool IsUnstripped
        {
            get => context.GetExtraBoolean("IsUnstripped");
            set => context.PutExtraBoolean("IsUnstripped", value);
        }

        /// <summary>
        /// This type or member is injected by the generator as a helper method. For example, static constructors to trigger Il2Cpp initialization.
        /// </summary>
        /// <remarks>
        /// For memory efficiency, this is only set at the top-level. If a type is injected, its members are not marked as injected, even though they obviously are.
        /// For the purpose of this property, nested types are not considered "members" and must be marked as injected separately.
        /// </remarks>
        public bool IsInjected
        {
            get => context.GetExtraBoolean("IsInjected") || (context.Parent?.IsInjected ?? false);
            set => context.PutExtraBoolean("IsInjected", value);
        }

        public ContextWithDataStorage? Parent => context switch
        {
            MethodAnalysisContext method => method.DeclaringType,
            FieldAnalysisContext @field => @field.DeclaringType,
            PropertyAnalysisContext property => property.DeclaringType,
            EventAnalysisContext @event => @event.DeclaringType,
            TypeAnalysisContext type => (ContextWithDataStorage?)type.DeclaringType ?? type.DeclaringAssembly,
            _ => null,
        };

        public bool GetExtraBoolean(string key) => context.GetExtraData<object>(key) is true;
        public void PutExtraBoolean(string key, bool value) => context.PutExtraData(key, value ? True : False);
    }
}
