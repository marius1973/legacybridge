using VfpInventory.Domain;
using System.Linq;

namespace VfpInventory.Application;

public sealed class ProductService
{
    private readonly IProductRepository _repo;
    public ProductService(IProductRepository repo) => _repo = repo;

    public decimal CalcStockValue(decimal tnQty, decimal tnUnitCost)
    {
        var lnValue = (tnQty * tnUnitCost);
        if ((lnValue > 10000m))
        {
            lnValue = (lnValue * 1.02m);
        }
        else
        {
            lnValue = (lnValue + 5m);
        }
        return Math.Round(lnValue, (int)(2m), MidpointRounding.AwayFromZero);
    }

    public decimal ApplyDiscount(decimal tnAmount, decimal tnPercent)
    {
        if ((tnPercent > 50m))
        {
            tnPercent = 50m;
        }
        var lnResult = (tnAmount - ((tnAmount * tnPercent) / 100m));
        return lnResult;
    }

    public void RevalueAll()
    {
        foreach (var item in _repo.GetAll().Where(item => (item.Stock > 0m)))
        {
            item.TotalValue = item.Stock * item.UnitCost;
        }
        _repo.Save();
    }

    public IReadOnlyList<Product> MonthlyReport(decimal tnYear)
    {
        // SELECT product , SUM ( total_value ) FROM products WHERE year = tnYear GROUP BY product ORDER BY 2 DESC
        return _repo.GetAll();
    }

}
