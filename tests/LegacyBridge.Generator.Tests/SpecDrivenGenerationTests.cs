using LegacyBridge.Generator.Spec;
using LegacyBridge.Parser.Parsing;
using Xunit;

namespace LegacyBridge.Generator.Tests;

public class SpecDrivenGenerationTests
{
    private const string VfpSample = """
        PROCEDURE CalcStockValue
            LPARAMETERS tnQty, tnUnitCost
            LOCAL lnValue
            lnValue = tnQty * tnUnitCost
            IF lnValue > 10000
                lnValue = lnValue * 1.02
            ELSE
                lnValue = lnValue + 5
            ENDIF
            RETURN ROUND(lnValue, 2)
        ENDPROC
        """;

    private const string SpecYaml = """
        source: inv_calc.prg
        entities:
          - name: Product
            fields: [product, stock, unit_cost, total_value, year]
        rules:
          - id: R1
            description: High-value stock gets a 2% surcharge
            routine: CalcStockValue
        flows:
          - name: CalcStockValue
            kind: procedure
            parameters: [tnQty, tnUnitCost]
        queries:
          - routine: MonthlyReport
            sql: SELECT product FROM products
        """;

    [Fact]
    public void SpecReader_parses_the_agent_spec_shape()
    {
        var spec = SpecReader.Parse(SpecYaml);

        Assert.Equal("inv_calc.prg", spec.Source);
        var entity = Assert.Single(spec.Entities);
        Assert.Equal("Product", entity.Name);
        Assert.Equal(5, entity.Fields.Count);
        Assert.Contains("unit_cost", entity.Fields);
        var rule = Assert.Single(spec.Rules);
        Assert.Equal("R1", rule.Id);
        Assert.Equal("CalcStockValue", rule.Routine);
        Assert.Single(spec.Flows);
        Assert.Single(spec.Queries);
    }

    private const string NestedListYaml = """
        source: inventory.prg
        entities:
          - name: Product
            fields:
              - product
              - stock
              - unit_cost
              - total_value
              - year
        rules:
          - id: R1
            description: High-value stock gets a 2% surcharge
            routine: CalcStockValue
        flows:
          - name: CalcStockValue
            kind: procedure
            parameters: []
        queries:
          - routine: MonthlyReport
            sql: SELECT product , SUM ( total_value ) FROM products WHERE year = tnYear
              GROUP BY product ORDER BY 2 DESC
        """;

    [Fact]
    public void SpecReader_parses_nested_lists_from_the_extractor()
    {
        var spec = SpecReader.Parse(NestedListYaml);

        var entity = Assert.Single(spec.Entities);
        Assert.Equal("Product", entity.Name);
        Assert.Equal(["product", "stock", "unit_cost", "total_value", "year"], entity.Fields);
        Assert.Single(spec.Rules);
        Assert.Single(spec.Flows);
        var query = Assert.Single(spec.Queries);
        Assert.Contains("GROUP BY", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_with_spec_uses_spec_entities_and_fields()
    {
        var program = VfpParser.Parse(VfpSample, "sample.prg");
        var spec = SpecReader.Parse(SpecYaml);
        var dir = Path.Combine(Path.GetTempPath(), "lb-spec-" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = SolutionGenerator.Write([program], dir, spec: spec);

            var entityFile = Path.Combine(dir, "Domain", "Product.cs");
            Assert.True(File.Exists(entityFile), $"expected {entityFile}");
            var content = File.ReadAllText(entityFile);
            Assert.Contains("class Product", content);
            Assert.Contains("UnitCost", content);
            Assert.Contains("TotalValue", content);
            if (GoldenCases.Load().Count > 0)
                Assert.Contains(result.Files, f => f.Contains("Tests", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Generate_without_spec_falls_back_to_heuristics()
    {
        var program = VfpParser.Parse(VfpSample, "sample.prg");
        var dir = Path.Combine(Path.GetTempPath(), "lb-nospec-" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = SolutionGenerator.Write([program], dir, spec: null);
            Assert.NotEmpty(result.Files);
            Assert.Contains(result.Files, f => f.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
