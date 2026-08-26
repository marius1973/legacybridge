namespace VfpInventory.Domain;

public sealed class Product
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Stock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public decimal Year { get; set; }
}