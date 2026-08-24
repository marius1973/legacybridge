namespace VfpInventory.Domain;

public sealed class Product
{
    public int Id { get; set; }
    public decimal Into { get; set; }
    public decimal LsDummy { get; set; }
    public string ProductName { get; set; } = "";
    public decimal TotalValue { get; set; }
    public decimal Year { get; set; }
}