namespace VfpInventory.Domain;

public interface IProductRepository
{
    IReadOnlyList<Product> GetAll();
    void Save();
}