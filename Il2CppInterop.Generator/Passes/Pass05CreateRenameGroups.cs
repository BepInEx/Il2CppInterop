using System.Text;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass05CreateRenameGroups
{
    private static readonly string[] ClassAccessNames =
        {"Private", "Public", "NPublic", "NPrivate", "NProtected", "NInternal", "NFamAndAssem", "NFamOrAssem"};

    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var originalType in assemblyContext.OriginalAssembly.ManifestModule!.TopLevelTypes)
                ProcessType(context, originalType, false);

        var typesToRemove = context.RenameGroups.Where(it => it.Value.Count > 1).ToList();
        foreach (var keyValuePair in typesToRemove)
        {
            context.RenameGroups.Remove(keyValuePair.Key);
            foreach (var typeDefinition in keyValuePair.Value)
                context.RenamedTypes.Remove(typeDefinition);
        }

        foreach (var contextRenamedType in context.RenamedTypes)
            context.PreviousRenamedTypes[contextRenamedType.Key] = contextRenamedType.Value;

        foreach (var assemblyContext in context.Assemblies)
            foreach (var originalType in assemblyContext.OriginalAssembly.ManifestModule!.TopLevelTypes)
                ProcessType(context, originalType, true);
    }

    private static void ProcessType(RewriteGlobalContext context, TypeDefinition originalType,
        bool allowExtraHeuristics)
    {
        foreach (var nestedType in originalType.NestedTypes)
            ProcessType(context, nestedType, allowExtraHeuristics);

        if (context.RenamedTypes.ContainsKey(originalType)) return;

        var unobfuscatedName = GetUnobfuscatedNameBase(context, originalType, allowExtraHeuristics);
        if (unobfuscatedName == null) return;

        context.RenameGroups
            .GetOrCreate(
                ((object?)originalType.DeclaringType ?? originalType.Namespace, unobfuscatedName,
                    originalType.GenericParameters.Count), _ => new List<TypeDefinition>()).Add(originalType);
        context.RenamedTypes[originalType] = unobfuscatedName;
    }

    private static string? GetUnobfuscatedNameBase(RewriteGlobalContext context, TypeDefinition typeDefinition,
        bool allowExtraHeuristics)
    {
        var options = context.Options;
        if (options.PassthroughNames || !typeDefinition.Name.IsObfuscated(context.Options)) return null;

        var inheritanceDepth = 0;
        var firstUnobfuscatedType = typeDefinition.BaseType;
        while (firstUnobfuscatedType != null && firstUnobfuscatedType.Name.IsObfuscated(context.Options))
        {
            firstUnobfuscatedType = firstUnobfuscatedType.Resolve()?.BaseType?.Resolve();
            inheritanceDepth++;
        }

        var unobfuscatedInterfacesList = typeDefinition.Interfaces.Select(it => it.Interface!)
            .Where(it => !it!.Name.IsObfuscated(context.Options));
        var accessName = ClassAccessNames[(int)(typeDefinition.Attributes & TypeAttributes.VisibilityMask)];

        var classifier = typeDefinition.IsInterface ? "Interface" : typeDefinition.IsValueType ? "Struct" : "Class";
        var compilerGenertaedString = typeDefinition.Name.StartsWith("<") ? "CompilerGenerated" : "";
        var abstractString = typeDefinition.IsAbstract ? "Abstract" : "";
        var sealedString = typeDefinition.IsSealed ? "Sealed" : "";
        var specialNameString = typeDefinition.IsSpecialName ? "SpecialName" : "";

        var nameBuilder = new StringBuilder();
        nameBuilder.Append(firstUnobfuscatedType?.ToTypeSignature().GenericNameToStrings(context)?.ConcatAll() ?? classifier);
        if (inheritanceDepth > 0)
            nameBuilder.Append(inheritanceDepth);
        nameBuilder.Append(compilerGenertaedString);
        nameBuilder.Append(accessName);
        nameBuilder.Append(abstractString);
        nameBuilder.Append(sealedString);
        nameBuilder.Append(specialNameString);
        foreach (var interfaceRef in unobfuscatedInterfacesList)
            nameBuilder.Append(interfaceRef.ToTypeSignature().GenericNameToStrings(context).ConcatAll());

        var uniqContext = new UniquificationContext(options);
        foreach (var fieldDef in typeDefinition.Fields)
        {
            if (!typeDefinition.IsEnum)
                uniqContext.Push(fieldDef.Signature!.FieldType.GenericNameToStrings(context));

            uniqContext.Push(fieldDef.Name!);

            if (uniqContext.CheckFull()) break;
        }

        if (typeDefinition.IsEnum)
            uniqContext.Push(typeDefinition.Fields.Count + "v");

        foreach (var propertyDef in typeDefinition.Properties)
        {
            uniqContext.Push(propertyDef.Signature!.ReturnType.GenericNameToStrings(context));
            uniqContext.Push(propertyDef.Name!);

            if (uniqContext.CheckFull()) break;
        }

        if (firstUnobfuscatedType?.Name == "MulticastDelegate")
        {
            var invokeMethod = typeDefinition.Methods.SingleOrDefault(it => it.Name == "Invoke");
            if (invokeMethod != null)
            {
                uniqContext.Push(invokeMethod.Signature!.ReturnType.GenericNameToStrings(context));

                foreach (var parameterDef in invokeMethod.Parameters)
                {
                    uniqContext.Push(parameterDef.ParameterType.GenericNameToStrings(context));
                    if (uniqContext.CheckFull()) break;
                }
            }
        }

        if (typeDefinition.IsInterface ||
            allowExtraHeuristics) // method order on non-interface types appears to be unstable
            foreach (var methodDefinition in typeDefinition.Methods)
            {
                uniqContext.Push(methodDefinition.Name!);
                uniqContext.Push(methodDefinition.Signature!.ReturnType.GenericNameToStrings(context));

                foreach (var parameter in methodDefinition.Parameters)
                {
                    uniqContext.Push(parameter.Name);
                    uniqContext.Push(parameter.ParameterType.GenericNameToStrings(context));

                    if (uniqContext.CheckFull()) break;
                }

                if (uniqContext.CheckFull()) break;
            }

        nameBuilder.Append(uniqContext.GetTop());

        return nameBuilder.ToString();
    }

    private static string ConcatAll(this List<string> strings)
    {
        return string.Concat(strings);
    }

    private static string NameOrRename(this TypeSignature typeRef, RewriteGlobalContext context)
    {
        var resolved = typeRef.Resolve();
        if (resolved != null && context.PreviousRenamedTypes.TryGetValue(resolved, out var rename))
            return (rename.StableHash() % (ulong)Math.Pow(10, context.Options.TypeDeobfuscationCharsPerUniquifier))
                .ToString();

        return typeRef.Name!;
    }

    private static List<string> GenericNameToStrings(this TypeSignature typeRef, RewriteGlobalContext context)
    {
        if (typeRef is SzArrayTypeSignature szArrayType)
            return szArrayType.BaseType.GenericNameToStrings(context);

        if (typeRef is ArrayTypeSignature arrayType)
            return arrayType.BaseType.GenericNameToStrings(context);

        if (typeRef is GenericInstanceTypeSignature genericInstance)
        {
            var baseTypeName = genericInstance.GenericType.ToTypeSignature().NameOrRename(context);
            var indexOfBacktick = baseTypeName.IndexOf('`');
            if (indexOfBacktick >= 0)
                baseTypeName = baseTypeName.Substring(0, indexOfBacktick);

            var entries = new List<string>();

            entries.Add(baseTypeName);
            entries.Add(genericInstance.TypeArguments.Count.ToString());
            foreach (var genericArgument in genericInstance.TypeArguments)
                entries.AddRange(genericArgument.GenericNameToStrings(context));
            return entries;
        }

        if (typeRef.NameOrRename(context).IsObfuscated(context.Options))
            return new List<string> { "Obf" };

        return new List<string> { typeRef.NameOrRename(context) };
    }
}
