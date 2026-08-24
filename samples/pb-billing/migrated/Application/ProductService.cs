using VfpInventory.Domain;
using System.Linq;

namespace VfpInventory.Application;

public sealed class ProductService
{
    private readonly IProductRepository _repo;
    public ProductService(IProductRepository repo) => _repo = repo;

    public decimal CalcStockValue(decimal tnQty, decimal tnUnitCost)
    {
        var ld_value = (tnQty * tnUnitCost);
        if ((ld_value > 10000m))
        {
            ld_value = (ld_value * 1.02m);
        }
        else
        {
            ld_value = (ld_value + 5m);
        }
        return Math.Round(ld_value, (int)(2m), MidpointRounding.AwayFromZero);
    }

    public decimal ApplyDiscount(decimal tnAmount, decimal tnPercent)
    {
        if ((tnPercent > 50m))
        {
            tnPercent = 50m;
        }
        var ld_result = (tnAmount - ((tnAmount * tnPercent) / 100m));
        return ld_result;
    }

    public IReadOnlyList<Product> MonthlyReport(decimal tnYear)
    {
        // select product , sum ( total_value ) into ls_dummy from products where year = tnYear
        return _repo.GetAll();
    }

}
