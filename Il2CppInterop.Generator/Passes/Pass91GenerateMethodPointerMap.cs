using System.Reflection;
using System.Text;
using Il2CppInterop.Common.Maps;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Passes;

public static class Pass91GenerateMethodPointerMap
{
    public static void DoPass(RewriteGlobalContext context, GeneratorOptions options)
    {
        var data = new List<(long, int, int)>();
        var assemblyList = new List<string>();

        foreach (var assemblyRewriteContext in context.Assemblies)
        {
            if (options.AdditionalAssembliesBlacklist.Contains(assemblyRewriteContext.NewAssembly.Name!))
                continue;

            assemblyList.Add(assemblyRewriteContext.NewAssembly.FullName);

            foreach (var typeRewriteContext in assemblyRewriteContext.Types)
                foreach (var methodRewriteContext in typeRewriteContext.Methods)
                {
                    var address = methodRewriteContext.Rva;

                    if (address != 0)
                        data.Add((address, methodRewriteContext.NewMethod.MetadataToken.ToInt32(), assemblyList.Count - 1));
                }
        }

        data.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        var header = new MethodAddressToTokenMapFileHeader
        {
            Magic = MethodAddressToTokenMapBase<Assembly, MethodBase>.Magic,
            Version = MethodAddressToTokenMapBase<Assembly, MethodBase>.Version,
            NumMethods = data.Count,
            NumAssemblies = assemblyList.Count
        };

        using var writer =
            new BinaryWriter(
                new FileStream(
                    Path.Combine(options.OutputDir, MethodAddressToTokenMapBase<Assembly, MethodBase>.FileName),
                    FileMode.Create, FileAccess.Write), Encoding.UTF8, false);
        writer.Write(header);

        foreach (var s in assemblyList)
            writer.Write(s);

        header.DataOffset = (int)writer.BaseStream.Position;

        foreach (var valueTuple in data)
            writer.Write(valueTuple.Item1);

        foreach (var valueTuple in data)
        {
            writer.Write(valueTuple.Item2);
            writer.Write(valueTuple.Item3);
        }

        writer.BaseStream.Position = 0;
        writer.Write(header);

        if (options.Verbose)
        {
            using var plainTextWriter = new StreamWriter(Path.Combine(options.OutputDir,
                MethodAddressToTokenMapBase<Assembly, MethodBase>.FileName + ".txt"));
            for (var i = 0; i < data.Count; i++)
                plainTextWriter.WriteLine($"{i}\t{data[i].Item1}\t{data[i].Item2}\t{data[i].Item3}");
        }
    }
}
