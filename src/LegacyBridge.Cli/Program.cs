using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Parsing;

// LegacyBridge CLI — v0.1
// usage: legacybridge analyze <file-or-directory> [--output ir.json]

return Cli.Run(args);

internal static class Cli
{
    public static int Run(string[] args)
    {
        if (args.Length < 2 || args[0] != "analyze")
        {
            Console.Error.WriteLine("usage: legacybridge analyze <file-or-directory> [--output ir.json]");
            return 2;
        }

        var path = args[1];
        var outputIdx = Array.IndexOf(args, "--output");
        var outputPath = outputIdx >= 0 && outputIdx + 1 < args.Length
            ? args[outputIdx + 1]
            : null;

        var files = Directory.Exists(path)
            ? Directory.GetFiles(path, "*.prg", SearchOption.AllDirectories)
            : new[] { path };

        var programs = new List<IrProgram>();
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var program = VfpParser.Parse(source, Path.GetFileName(file));
            programs.Add(program);
            Console.WriteLine($"parsed {file}: {program.Routines.Count} routine(s)");
        }

        var json = programs.Count == 1
            ? IrSerializer.ToJson(programs[0])
            : System.Text.Json.JsonSerializer.Serialize(programs,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        if (outputPath is not null)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (dir is not null)
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, json);
            Console.WriteLine($"IR written to {outputPath}");
        }
        else
        {
            Console.WriteLine(json);
        }

        return 0;
    }
}
