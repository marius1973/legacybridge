using System.Diagnostics;
using LegacyBridge.Equivalence;
using LegacyBridge.Generator;
using LegacyBridge.Parser.Ir;
using LegacyBridge.Parser.Lexing;
using LegacyBridge.Parser.Parsing;

return Cli.Run(args);

internal static class Cli
{
    private const string Usage =
        "usage:\n  legacybridge analyze <path> [--output ir.json] [--strict]\n  legacybridge extract <path> [--output spec.yaml] [--strict] [--llm]\n  legacybridge generate <path> [--output dir] [--strict] [--build]\n  legacybridge verify <path> [--output EQUIVALENCE-REPORT.md] [--min-match 0.9]";

    public static int Run(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("analyze" or "extract" or "generate" or "verify"))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var cmd = args[0];
        var strict = false;
        var llm = false;
        var build = false;
        var minMatch = 0.9;
        string? path = null;
        string? outputPath = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--strict") { strict = true; continue; }
            if (args[i] == "--llm") { llm = true; continue; }
            if (args[i] == "--build") { build = true; continue; }
            if (args[i] == "--min-match")
            {
                if (i + 1 >= args.Length || !double.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out minMatch))
                {
                    Console.Error.WriteLine(Usage);
                    return 2;
                }
                continue;
            }
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

        if (cmd == "generate")
            return Generate(programs, outputPath ?? "generated", build);

        if (cmd == "verify")
            return Verify(programs, outputPath, minMatch);

        return Extract(programs, outputPath, llm);
    }

    private static List<IrProgram> ParseAll(string path, bool strict)
    {
        IEnumerable<string> files = Directory.Exists(path)
            ? Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(SourceParser.IsLegacySource)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            : [path];
        return files
            .Select(file => SourceParser.Parse(File.ReadAllText(file), Path.GetFileName(file), strict))
            .ToList();
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

    private static int Generate(List<IrProgram> programs, string outputDir, bool build)
    {
        var result = SolutionGenerator.Write(programs, outputDir);
        Console.WriteLine($"generated {result.Files.Count} files → {result.SlnPath}");
        if (!build)
            return 0;
        var (ok, log, attempts) = SolutionGenerator.Build(result.SlnPath);
        Console.WriteLine($"build {(ok ? "ok" : "FAIL")} in {attempts} attempt(s)");
        if (!ok) Console.Error.Write(log);
        return ok ? 0 : 1;
    }

    private static int Verify(List<IrProgram> programs, string? outputPath, double minMatch)
    {
        var program = programs[0];
        var report = Verifier.Run(program);
        var md = Verifier.ToMarkdown(report, program.SourceName);
        var dest = outputPath ?? "EQUIVALENCE-REPORT.md";
        var dir = Path.GetDirectoryName(Path.GetFullPath(dest));
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(dest, md);
        Console.WriteLine($"equivalence {(report.Rate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}% ({report.Matched}/{report.Matched + report.Mismatched}, skip {report.Skipped}) → {dest}");
        if (report.Rate + 1e-9 < minMatch)
        {
            Console.Error.WriteLine($"match rate {(report.Rate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}% is below {(minMatch * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%");
            return 1;
        }
        return 0;
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
