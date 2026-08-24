using System.Diagnostics;
using System.Text;
using LegacyBridge.Generator.Spec;
using LegacyBridge.Parser.Ir;

namespace LegacyBridge.Generator;

public sealed record GenerateResult(string SlnPath, IReadOnlyList<string> Files);

public static class SolutionGenerator
{
    public static GenerateResult Write(
        IReadOnlyList<IrProgram> programs,
        string outputDir,
        string ns = "VfpInventory",
        BusinessSpec? spec = null)
    {
        outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputDir);
        var entities = spec is { Entities.Count: > 0 }
            ? spec.Entities.Select(e => new EntityModel(e.Name, e.Fields.ToList())).ToList()
            : SpecInfer.Entities(programs);
        var entity = entities.FirstOrDefault();
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var domainCsproj = $"{ns}.Domain.csproj";
        var appCsproj = $"{ns}.Application.csproj";
        var infCsproj = $"{ns}.Infrastructure.csproj";
        var apiCsproj = $"{ns}.Api.csproj";
        var testCsproj = $"{ns}.Tests.csproj";
        var dGuid = Guid.NewGuid();
        var aGuid = Guid.NewGuid();
        var iGuid = Guid.NewGuid();
        var pGuid = Guid.NewGuid();
        var tGuid = Guid.NewGuid();

        files[$"Domain/{domainCsproj}"] = SdkCsproj();
        files[$"Application/{appCsproj}"] = SdkCsproj($"    <ProjectReference Include=\"..\\Domain\\{domainCsproj}\" />");
        files[$"Infrastructure/{infCsproj}"] = SdkCsproj(
            $"    <ProjectReference Include=\"..\\Domain\\{domainCsproj}\" />",
            """
                <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.8" />
                <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.8" />
            """);
        files[$"Api/{apiCsproj}"] = WebCsproj(
            $"    <ProjectReference Include=\"..\\Application\\{appCsproj}\" />",
            $"    <ProjectReference Include=\"..\\Infrastructure\\{infCsproj}\" />",
            """
                <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.8" />
            """);

        if (entity is not null)
        {
            files[$"Domain/{entity.Name}.cs"] = EntityFile(ns, entity);
            files[$"Domain/I{entity.Name}Repository.cs"] = RepoIface(ns, entity.Name);
            files[$"Infrastructure/AppDbContext.cs"] = DbContextFile(ns, entity.Name);
            files[$"Infrastructure/{entity.Name}Repository.cs"] = RepoImpl(ns, entity.Name);
        }

        files[$"Application/{ServiceName(entity)}.cs"] = ServiceFile(ns, programs, entity);
        files["Api/Program.cs"] = ApiFile(ns, entity, programs);
        var goldens = GoldenCases.Load().Where(g => programs.SelectMany(p => p.Routines).Any(r => r.Name == g.Routine)).ToList();
        var needsRepo = programs.SelectMany(p => p.Routines).Any(NeedsRepo);
        if (goldens.Count > 0 && entity is not null)
        {
            files[$"Tests/{testCsproj}"] = TestCsproj(ns, appCsproj, domainCsproj);
            files[$"Tests/{ServiceName(entity)}Tests.cs"] = TestFile(ns, entity, goldens, needsRepo);
        }
        files[$"{ns}.sln"] = Sln(ns, domainCsproj, appCsproj, infCsproj, apiCsproj, testCsproj,
            dGuid, aGuid, iGuid, pGuid, tGuid, goldens.Count > 0 && entity is not null);

        foreach (var (rel, content) in files)
        {
            var path = Path.Combine(outputDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
        }

        return new GenerateResult(Path.Combine(outputDir, $"{ns}.sln"), files.Keys.ToList());
    }

    public static (bool Ok, string Log, int Attempts) Build(string slnPath, int maxAttempts = 1)
    {
        var log = new StringBuilder();
        for (int i = 1; i <= maxAttempts; i++)
        {
            var psi = new ProcessStartInfo
            {
                FileName = DotnetPath(),
                WorkingDirectory = Path.GetDirectoryName(slnPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("build");
            psi.ArgumentList.Add(slnPath);
            psi.ArgumentList.Add("--nologo");
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            log.AppendLine($"--- attempt {i} exit {p.ExitCode} ---");
            log.Append(stdout);
            log.Append(stderr);
            if (p.ExitCode == 0)
                return (true, log.ToString(), i);
            // LLM repair hook: mutate source from compiler log, then retry. Not wired yet.
        }
        return (false, log.ToString(), maxAttempts);
    }

    private static string DotnetPath() => DotnetLocator.Find();

    private static string ServiceName(EntityModel? e) => e is null ? "LegacyService" : e.Name + "Service";

    private static string SdkCsproj(string extraRef = "", string extraPkg = "")
    {
        var extras = string.Join("\n", new[] { extraRef, extraPkg }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var items = extras.Length == 0 ? "" : $"""

          <ItemGroup>
        {extras}
          </ItemGroup>
        """;
        return $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>{items}
        </Project>
        """;
    }

    private static string WebCsproj(params string[] refs) => $"""
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
        {string.Join("\n", refs)}
          </ItemGroup>
        </Project>
        """;

    private static string EntityFile(string ns, EntityModel e)
    {
        var props = new StringBuilder();
        props.AppendLine("    public int Id { get; set; }");
        foreach (var f in e.Fields)
        {
            var clr = Names.ClrType(f);
            var name = Names.Property(e.Name, f);
            props.AppendLine(clr == "string"
                ? $"    public string {name} {{ get; set; }} = \"\";"
                : $"    public {clr} {name} {{ get; set; }}");
        }
        return $$"""
            namespace {{ns}}.Domain;

            public sealed class {{e.Name}}
            {
            {{props.ToString().TrimEnd()}}
            }
            """;
    }

    private static string RepoIface(string ns, string entity) => $$"""
        namespace {{ns}}.Domain;

        public interface I{{entity}}Repository
        {
            IReadOnlyList<{{entity}}> GetAll();
            void Save();
        }
        """;

    private static string DbContextFile(string ns, string entity) => $$"""
        using Microsoft.EntityFrameworkCore;
        using {{ns}}.Domain;

        namespace {{ns}}.Infrastructure;

        public sealed class AppDbContext : DbContext
        {
            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
            public DbSet<{{entity}}> {{entity}}s => Set<{{entity}}>();
        }
        """;

    private static string RepoImpl(string ns, string entity) => $$"""
        using {{ns}}.Domain;

        namespace {{ns}}.Infrastructure;

        public sealed class {{entity}}Repository : I{{entity}}Repository
        {
            private readonly AppDbContext _db;
            public {{entity}}Repository(AppDbContext db) => _db = db;
            public IReadOnlyList<{{entity}}> GetAll() => _db.{{entity}}s.ToList();
            public void Save() => _db.SaveChanges();
        }
        """;

    private static string ServiceFile(string ns, IReadOnlyList<IrProgram> programs, EntityModel? entity)
    {
        var name = ServiceName(entity);
        var needsRepo = programs.SelectMany(p => p.Routines).Any(NeedsRepo);
        var sb = new StringBuilder();
        sb.AppendLine($"using {ns}.Domain;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns}.Application;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {name}");
        sb.AppendLine("{");
        if (needsRepo && entity is not null)
        {
            sb.AppendLine($"    private readonly I{entity.Name}Repository _repo;");
            sb.AppendLine($"    public {name}(I{entity.Name}Repository repo) => _repo = repo;");
            sb.AppendLine();
        }
        foreach (var r in programs.SelectMany(p => p.Routines))
        {
            var ret = ReturnType(r, entity);
            var pars = string.Join(", ", r.Parameters.Select(p => $"decimal {Names.Ident(p)}"));
            sb.AppendLine($"    public {ret} {r.Name}({pars})");
            sb.AppendLine("    {");
            var body = CsharpEmitter.MethodBody(r, entity?.Name);
            if (string.IsNullOrWhiteSpace(body))
                sb.AppendLine(ret == "void" ? "        return;" : "        return default;");
            else
                sb.AppendLine(body);
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool NeedsRepo(IrRoutine r) =>
        Flatten(r.Body).Any(s => s.Kind is "scan" or "sql"
            || (s.Expression?.RawText ?? "").StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase)
            || (s.Expression?.RawText ?? "").StartsWith("USE", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<IrStatement> Flatten(IReadOnlyList<IrStatement>? body)
    {
        foreach (var s in body ?? [])
        {
            yield return s;
            foreach (var c in Flatten(s.Then)) yield return c;
            foreach (var c in Flatten(s.Else)) yield return c;
            foreach (var c in Flatten(s.Body)) yield return c;
        }
    }

    private static string ReturnType(IrRoutine r, EntityModel? entity)
    {
        if (Flatten(r.Body).Any(s => s.Kind == "return" && s.Expression is not null))
            return "decimal";
        if (entity is not null && Flatten(r.Body).Any(s => s.Kind == "sql"))
            return $"IReadOnlyList<{entity.Name}>";
        return "void";
    }

    private static string ApiFile(string ns, EntityModel? entity, IReadOnlyList<IrProgram> programs)
    {
        var svc = ServiceName(entity);
        var di = entity is null ? "" : $"""
            b.Services.AddDbContext<{ns}.Infrastructure.AppDbContext>(o =>
                o.UseInMemoryDatabase("{ns}"));
            b.Services.AddScoped<{ns}.Domain.I{entity.Name}Repository, {ns}.Infrastructure.{entity.Name}Repository>();
            """;
        var maps = new StringBuilder();
        foreach (var r in programs.SelectMany(p => p.Routines))
        {
            var route = "/" + Kebab(r.Name);
            var args = string.Join(", ", r.Parameters.Select(p => $"decimal {Names.Ident(p)}"));
            var call = string.Join(", ", r.Parameters.Select(Names.Ident));
            var inject = args.Length > 0 ? args + ", " + svc + " s" : svc + " s";
            var ret = ReturnType(r, entity);
            if (ret == "void")
                maps.AppendLine($"app.MapPost(\"{route}\", ({inject}) => {{ s.{r.Name}({call}); return Results.Ok(); }});");
            else
                maps.AppendLine($"app.MapGet(\"{route}\", ({inject}) => s.{r.Name}({call}));");
        }
        return $$"""
            using Microsoft.EntityFrameworkCore;
            using {{ns}}.Application;
            {{(entity is null ? "" : $"using {ns}.Infrastructure;")}}

            var b = WebApplication.CreateBuilder(args);
            {{di}}
            b.Services.AddScoped<{{svc}}>();
            var app = b.Build();
            {{maps.ToString().TrimEnd()}}
            app.Run();
            """;
    }

    private static string Kebab(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private static string TestCsproj(string ns, string appCsproj, string domainCsproj) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
            <PackageReference Include="xunit" Version="2.9.2" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="..\Application\{appCsproj}" />
            <ProjectReference Include="..\Domain\{domainCsproj}" />
          </ItemGroup>
        </Project>
        """;

    private static string TestFile(string ns, EntityModel entity, IReadOnlyList<GoldenCase> goldens, bool needsRepo)
    {
        var svc = ServiceName(entity);
        var groups = goldens.GroupBy(g => g.Routine, StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine($"using {ns}.Application;");
        sb.AppendLine($"using {ns}.Domain;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns}.Tests;");
        sb.AppendLine();
        sb.AppendLine($"public class {svc}Tests");
        sb.AppendLine("{");
        sb.AppendLine(needsRepo
            ? $"    private static {svc} Svc() => new(new MemRepo());"
            : $"    private static {svc} Svc() => new();");
        sb.AppendLine();
        foreach (var g in groups)
        {
            var routine = g.Key;
            var sample = g.First();
            var paramList = string.Join(", ", sample.Args.Keys.Select(k => $"decimal {k}"));
            var call = string.Join(", ", sample.Args.Keys);
            sb.AppendLine("    [Theory]");
            foreach (var c in g)
            {
                var nums = string.Join(", ", c.Args.Values.Append(c.Expected).Select(GoldenCases.Invariant));
                sb.AppendLine($"    [InlineData({nums})]");
            }
            sb.AppendLine($"    public void {routine}_matches_hand_computed_oracle({paramList}, decimal expected)");
            sb.AppendLine("    {");
            sb.AppendLine($"        Assert.Equal(expected, Svc().{routine}({call}));");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        if (needsRepo)
        {
            sb.AppendLine("    private sealed class MemRepo : I" + entity.Name + "Repository");
            sb.AppendLine("    {");
            sb.AppendLine($"        public IReadOnlyList<{entity.Name}> GetAll() => Array.Empty<{entity.Name}>();");
            sb.AppendLine("        public void Save() { }");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Sln(string ns, string d, string a, string i, string p, string t,
        Guid dg, Guid ag, Guid ig, Guid pg, Guid tg, bool includeTests)
    {
        const string cs = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        string G(Guid g) => "{" + g.ToString().ToUpperInvariant() + "}";
        string cfg(Guid g)
        {
            var id = G(g);
            return string.Join("\n",
                $"\t\t{id}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                $"\t\t{id}.Debug|Any CPU.Build.0 = Debug|Any CPU",
                $"\t\t{id}.Release|Any CPU.ActiveCfg = Release|Any CPU",
                $"\t\t{id}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        var lines = new List<string>
        {
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            "# Visual Studio Version 17",
            $"Project(\"{cs}\") = \"{ns}.Domain\", \"Domain\\{d}\", \"{G(dg)}\"",
            "EndProject",
            $"Project(\"{cs}\") = \"{ns}.Application\", \"Application\\{a}\", \"{G(ag)}\"",
            "EndProject",
            $"Project(\"{cs}\") = \"{ns}.Infrastructure\", \"Infrastructure\\{i}\", \"{G(ig)}\"",
            "EndProject",
            $"Project(\"{cs}\") = \"{ns}.Api\", \"Api\\{p}\", \"{G(pg)}\"",
            "EndProject",
        };
        if (includeTests)
        {
            lines.Add($"Project(\"{cs}\") = \"{ns}.Tests\", \"Tests\\{t}\", \"{G(tg)}\"");
            lines.Add("EndProject");
        }
        lines.Add("Global");
        lines.Add("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        lines.Add("\t\tDebug|Any CPU = Debug|Any CPU");
        lines.Add("\t\tRelease|Any CPU = Release|Any CPU");
        lines.Add("\tEndGlobalSection");
        lines.Add("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        lines.Add(cfg(dg));
        lines.Add(cfg(ag));
        lines.Add(cfg(ig));
        lines.Add(cfg(pg));
        if (includeTests) lines.Add(cfg(tg));
        lines.Add("\tEndGlobalSection");
        lines.Add("EndGlobal");
        return string.Join("\n", lines);
    }
}
