using Microsoft.EntityFrameworkCore;
using VfpInventory.Application;
using VfpInventory.Infrastructure;

var b = WebApplication.CreateBuilder(args);
b.Services.AddDbContext<VfpInventory.Infrastructure.AppDbContext>(o =>
    o.UseInMemoryDatabase("VfpInventory"));
b.Services.AddScoped<VfpInventory.Domain.IProductRepository, VfpInventory.Infrastructure.ProductRepository>();
b.Services.AddScoped<ProductService>();
var app = b.Build();
app.MapGet("/calc-stock-value", (decimal tnQty, decimal tnUnitCost, ProductService s) => s.CalcStockValue(tnQty, tnUnitCost));
app.MapGet("/apply-discount", (decimal tnAmount, decimal tnPercent, ProductService s) => s.ApplyDiscount(tnAmount, tnPercent));
app.MapPost("/revalue-all", (ProductService s) => { s.RevalueAll(); return Results.Ok(); });
app.MapPost("/purge-stale", (ProductService s) => { s.PurgeStale(); return Results.Ok(); });
app.MapGet("/monthly-report", (decimal tnYear, ProductService s) => s.MonthlyReport(tnYear));
app.Run();