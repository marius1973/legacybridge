using Microsoft.EntityFrameworkCore;
using VfpInventory.Domain;

namespace VfpInventory.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Product> Products => Set<Product>();
}