using VfpInventory.Application;
using VfpInventory.Domain;
using Xunit;

namespace VfpInventory.Tests;

public class ProductServiceTests
{
    private static ProductService Svc() => new(new MemRepo());

    [Theory]
    [InlineData(1, 10, 15)]
    [InlineData(100, 150, 15300)]
    [InlineData(100, 100, 10005)]
    [InlineData(0, 0, 5)]
    public void CalcStockValue_matches_hand_computed_oracle(decimal tnQty, decimal tnUnitCost, decimal expected)
    {
        Assert.Equal(expected, Svc().CalcStockValue(tnQty, tnUnitCost));
    }

    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(100, 60, 50)]
    [InlineData(100, 0, 100)]
    public void ApplyDiscount_matches_hand_computed_oracle(decimal tnAmount, decimal tnPercent, decimal expected)
    {
        Assert.Equal(expected, Svc().ApplyDiscount(tnAmount, tnPercent));
    }

    private sealed class MemRepo : IProductRepository
    {
        public IReadOnlyList<Product> GetAll() => Array.Empty<Product>();
        public void Save() { }
    }
}
