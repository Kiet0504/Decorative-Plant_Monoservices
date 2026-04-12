using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Infrastructure.Data;
using System.Text.Json;

// This is a scratch script to inspect the database state for a specific batch
var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql("Host=localhost;Database=decorativeplant_db;Username=postgres;Password=postgres");

using var context = new ApplicationDbContext(optionsBuilder.Options, null);

var batchCode = "AUTO-B81108AE";
var batch = await context.PlantBatches
    .Include(x => x.Taxonomy)
    .Include(x => x.ProductListings)
    .Include(x => x.BatchStocks)
        .ThenInclude(s => s.Location)
    .FirstOrDefaultAsync(x => x.BatchCode == batchCode);

if (batch == null)
{
    Console.WriteLine($"Batch {batchCode} not found.");
    return;
}

Console.WriteLine($"Batch: {batch.BatchCode} (ID: {batch.Id})");
Console.WriteLine($"Nursery Qty: {batch.CurrentTotalQuantity}");
Console.WriteLine($"Taxonomy: {batch.Taxonomy?.ScientificName}");
if (batch.Taxonomy != null && batch.Taxonomy.CommonNames != null)
{
     Console.WriteLine($"Common Names: {batch.Taxonomy.CommonNames.RootElement.GetRawText()}");
}

Console.WriteLine("\n--- Product Listings ---");
foreach (var pl in batch.ProductListings)
{
    Console.WriteLine($"ID: {pl.Id}, BranchId: {pl.BranchId}");
    if (pl.ProductInfo != null)
        Console.WriteLine($"Title: {pl.ProductInfo.RootElement.GetProperty("title").GetString()}");
}

Console.WriteLine("\n--- Batch Stocks ---");
foreach (var s in batch.BatchStocks)
{
    Console.WriteLine($"ID: {s.Id}, Location: {s.Location?.Name} ({s.Location?.Type})");
    if (s.Quantities != null)
        Console.WriteLine($"Quantities: {s.Quantities.RootElement.GetRawText()}");
}
