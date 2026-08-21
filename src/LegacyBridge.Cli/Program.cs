using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Lexing;
using LegacyBridge.Parser.Parsing;

// usage: legacybridge analyze <file-or-directory> [--output ir.json] [--strict]

return Cli.Run(args);

internal static class Cli
{
    public static int Run(string[] args)
    {
        if (args.Length < 2 || args[0] != "analyze")
        {
            Console.Error.WriteLine("usage: legacybridge analyze <file-or-directory> [--output ir.json] [--strict]");
            return 2;
        }

        var strict = false;
        string? path = null;
        string? outputPath = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--strict") { strict = true; continue; }
            if (args[i] == "--output")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("usage: legacybridge analyze <file-or-directory> [--output ir.json] [--strict]");
                    return 2;
                }
                outputPath = args[++i];
                continue;
            }
            if (path is null) path = args[i];
        }

        if (path is null)
        {
            Console.Error.WriteLine("usage: legacybridge analyze <file-or-directory> [--output ir.json] [--strict]");
            return 2;
        }

        var files = Directory.Exists(path)
            ? Directory.GetFiles(path, "*.prg", SearchOption.AllDirectories)
            : new[] { path };

        var programs = new List<IrProgram>();
        try
        {
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                var program = VfpParser.Parse(source, Path.GetFileName(file), strict);
                programs.Add(program);
                Console.WriteLine($"parsed {file}: {program.Routines.Count} routine(s)");
            }
        }
        catch (Exception ex) when (ex is ParserException or LexerException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var json = programs.Count == 1
            ? IrSerializer.ToJson(programs[0])
            : IrSerializer.ToJson(programs);

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
