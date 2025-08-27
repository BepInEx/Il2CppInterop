using System.Reflection;
using System.Runtime.CompilerServices;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Il2CppInterop.Generator;

internal static class InjectedTypeAnalysisContextExtensions
{
    extension(InjectedTypeAnalysisContext type)
    {
        public void InjectContentFromSourceType()
        {
            var sourceType = type.SourceType;
            ArgumentNullException.ThrowIfNull(sourceType);

            var appContext = type.AppContext;

            type.SetDefaultBaseType(sourceType.BaseType != null
                ? new ContextResolver(type).ResolveOrThrow(sourceType.BaseType)
                : null);

            foreach (var fieldInfo in sourceType.GetFields())
            {
                if (fieldInfo.DeclaringType != sourceType)
                    continue;

                var resolver = new ContextResolver(type);

                type.InjectFieldContext(
                    fieldInfo.Name,
                    resolver.ResolveOrThrow(fieldInfo.FieldType),
                    fieldInfo.Attributes);
            }

            Dictionary<MethodBase, MethodAnalysisContext> methodMap = new();
            foreach (var method in GetPublicAndProtectedMethods(sourceType))
            {
                if (method.DeclaringType != sourceType)
                    continue;

                var parameterInfoArray = method.GetParameters();
                var parameterNames = parameterInfoArray
                    .Select(x => x.Name!)
                    .ToArray();
                var parameterAttributes = parameterInfoArray
                    .Select(x => x.Attributes)
                    .ToArray();
                var methodContext = new InjectedMethodAnalysisContext(
                    type,
                    method.Name,
                    appContext.SystemTypes.SystemObjectType,
                    method.Attributes,
                    Enumerable.Repeat(appContext.SystemTypes.SystemObjectType, parameterInfoArray.Length).ToArray(),
                    parameterNames,
                    parameterAttributes);
                if (method is not ConstructorInfo)
                {
                    foreach (var genericParameter in method.GetGenericArguments())
                    {
                        var genericParameterContext = new GenericParameterTypeAnalysisContext(genericParameter.Name, genericParameter.GenericParameterPosition, Il2CppTypeEnum.IL2CPP_TYPE_MVAR, genericParameter.GenericParameterAttributes, methodContext);
                        methodContext.GenericParameters.Add(genericParameterContext);
                    }
                }
                type.Methods.Add(methodContext);

                methodMap.Add(method, methodContext);

                var resolver = new ContextResolver(methodContext);

                var returnType = method switch
                {
                    MethodInfo methodInfo => resolver.ResolveOrThrow(methodInfo.ReturnType),
                    ConstructorInfo => appContext.SystemTypes.SystemVoidType,
                    _ => throw new NotSupportedException($"Unsupported method type: {method.GetType()}")
                };
                methodContext.SetDefaultReturnType(returnType);
                for (var i = 0; i < parameterInfoArray.Length; i++)
                {
                    var parameterType = resolver.ResolveOrThrow(parameterInfoArray[i].ParameterType);
                    var parameterContext = (InjectedParameterAnalysisContext)methodContext.Parameters[i];
                    parameterContext.SetDefaultParameterType(parameterType);
                }
            }

            foreach (var property in sourceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (property.DeclaringType != sourceType)
                    continue;

                var getMethod = methodMap.TryGetValue(property.GetMethod);
                var setMethod = methodMap.TryGetValue(property.SetMethod);
                if (getMethod == null && setMethod == null)
                    continue;

                var resolver = new ContextResolver(type);

                var propertyType = resolver.ResolveOrThrow(property.PropertyType);
                type.InjectPropertyContext(
                    property.Name,
                    propertyType,
                    getMethod,
                    setMethod,
                    property.Attributes);
            }

            foreach (var eventInfo in sourceType.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (eventInfo.DeclaringType != sourceType)
                    continue;

                var addMethod = methodMap.TryGetValue(eventInfo.AddMethod);
                var removeMethod = methodMap.TryGetValue(eventInfo.RemoveMethod);
                var raiseMethod = methodMap.TryGetValue(eventInfo.RaiseMethod);
                if (addMethod == null && removeMethod == null && raiseMethod == null)
                    continue;

                var resolver = new ContextResolver(type);
                var eventType = resolver.ResolveOrThrow(eventInfo.EventHandlerType!);
                type.InjectEventContext(
                    eventInfo.Name,
                    eventType,
                    addMethod,
                    removeMethod,
                    raiseMethod,
                    eventInfo.Attributes);
            }
        }

        public void SetDefaultBaseType(TypeAnalysisContext? baseType)
        {
            GetDefaultBaseType(type) = baseType;
        }
    }

    static IEnumerable<MethodBase> GetPublicAndProtectedMethods(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var constructor in constructors)
        {
            if (constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly)
            {
                yield return constructor;
            }
        }
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in methods)
        {
            if (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)
            {
                yield return method;
            }
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = $"<{nameof(InjectedTypeAnalysisContext.DefaultBaseType)}>k__BackingField")]
    private static extern ref TypeAnalysisContext? GetDefaultBaseType(InjectedTypeAnalysisContext context);
}
