using VfpInventory.Domain;

namespace VfpInventory.Infrastructure;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;
    public IReadOnlyList<Product> GetAll() => _db.Products.ToList();
    public void Save() => _db.SaveChanges();
}