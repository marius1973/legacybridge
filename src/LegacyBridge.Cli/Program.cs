using System.Diagnostics;
using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Lexing;
using LegacyBridge.Parser.Parsing;

return Cli.Run(args);

internal static class Cli
{
    private const string Usage =
        "usage:\n  legacybridge analyze <path> [--output ir.json] [--strict]\n  legacybridge extract <path> [--output spec.yaml] [--strict] [--llm]";

    public static int Run(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("analyze" or "extract"))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var cmd = args[0];
        var strict = false;
        var llm = false;
        string? path = null;
        string? outputPath = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--strict") { strict = true; continue; }
            if (args[i] == "--llm") { llm = true; continue; }
            if (args[i] == "--output")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine(Usage);
                    return 2;
                }
                outputPath = args[++i];
                continue;
            }
            path ??= args[i];
        }

        if (path is null)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        List<IrProgram> programs;
        try
        {
            programs = ParseAll(path, strict);
        }
        catch (Exception ex) when (ex is ParserException or LexerException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        foreach (var p in programs)
            Console.WriteLine($"parsed {p.SourceName}: {p.Routines.Count} routine(s)");

        if (cmd == "analyze")
        {
            WriteOutput(programs, outputPath);
            return 0;
        }

        return Extract(programs, outputPath, llm);
    }

    private static List<IrProgram> ParseAll(string path, bool strict)
    {
        var files = Directory.Exists(path)
            ? Directory.GetFiles(path, "*.prg", SearchOption.AllDirectories)
            : new[] { path };
        var programs = new List<IrProgram>();
        foreach (var file in files)
            programs.Add(VfpParser.Parse(File.ReadAllText(file), Path.GetFileName(file), strict));
        return programs;
    }

    private static void WriteOutput(List<IrProgram> programs, string? outputPath)
    {
        var json = programs.Count == 1 ? IrSerializer.ToJson(programs[0]) : IrSerializer.ToJson(programs);
        if (outputPath is null)
        {
            Console.WriteLine(json);
            return;
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"IR written to {outputPath}");
    }

    private static int Extract(List<IrProgram> programs, string? outputPath, bool llm)
    {
        var agents = FindAgentsDir();
        if (agents is null)
        {
            Console.Error.WriteLine("src/agents not found");
            return 1;
        }
        var tsx = Path.Combine(agents, "node_modules", "tsx", "dist", "cli.mjs");
        if (!File.Exists(tsx))
        {
            Console.Error.WriteLine("agents not installed — run: npm install --prefix src/agents");
            return 1;
        }

        var irPath = Path.GetTempFileName();
        var specPath = Path.GetFullPath(outputPath ?? Path.Combine(Path.GetTempPath(), "legacybridge-spec.yaml"));
        try
        {
            File.WriteAllText(irPath, programs.Count == 1 ? IrSerializer.ToJson(programs[0]) : IrSerializer.ToJson(programs));
            var specDir = Path.GetDirectoryName(Path.GetFullPath(specPath));
            if (specDir is not null) Directory.CreateDirectory(specDir);

            var psi = new ProcessStartInfo
            {
                FileName = "node",
                WorkingDirectory = agents,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add(tsx);
            psi.ArgumentList.Add("extract.ts");
            psi.ArgumentList.Add("--ir");
            psi.ArgumentList.Add(irPath);
            psi.ArgumentList.Add("--output");
            psi.ArgumentList.Add(specPath);
            if (llm)
            {
                psi.ArgumentList.Add("--llm");
                psi.Environment["LEGACYBRIDGE_LLM"] = "required";
            }

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                Console.Error.WriteLine("failed to start node");
                return 1;
            }
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (stderr.Length > 0) Console.Error.Write(stderr);
            if (proc.ExitCode != 0)
            {
                Console.Out.Write(stdout);
                return proc.ExitCode;
            }
            if (outputPath is null) Console.Write(File.ReadAllText(specPath));
            else Console.WriteLine($"spec written to {outputPath}");
            return 0;
        }
        finally
        {
            try { File.Delete(irPath); } catch { /* temp */ }
        }
    }

    private static string? FindAgentsDir()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var agents = Path.Combine(dir.FullName, "src", "agents");
                if (File.Exists(Path.Combine(agents, "extract.ts")))
                    return agents;
            }
        }
        return null;
    }
}
