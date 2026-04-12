using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace decorativeplant_be.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task Seed(ApplicationDbContext context)
    {
        // 1. Seed Plant Categories
        // We align these Slugs with the values in frontend's constants/categories.ts
        var categories = new List<PlantCategory>
        {
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000001"), Name = "Shade Loving", Slug = "shade_loving" },
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000002"), Name = "Indoor Plants", Slug = "indoor" },
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000003"), Name = "Outdoor Plants", Slug = "outdoor" },
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000004"), Name = "Succulents", Slug = "succulent" },
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000005"), Name = "Flowering", Slug = "flowering" },
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000006"), Name = "Hanging Plants", Slug = "hanging" },
            new() { Id = Guid.Parse("ca000001-0001-0001-0001-000000000007"), Name = "Low Maintenance", Slug = "low_maintenance" }
        };

        foreach (var cat in categories)
        {
            // Check by ID or Slug to avoid duplicates or handle re-slugging
            var existing = await context.PlantCategories
                .FirstOrDefaultAsync(c => c.Id == cat.Id || c.Slug == cat.Slug || (cat.Slug == "shade_loving" && c.Slug == "cay-ua-bong-chiu-bong"));
            
            if (existing == null)
            {
                context.PlantCategories.Add(cat);
            }
            else
            {
                // Update existing properties to match current code configuration
                existing.Slug = cat.Slug;
                existing.Name = cat.Name;
            }
        }

        await context.SaveChangesAsync();
    }
}
