using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.ShoppingCart.Commands;
using decorativeplant_be.Application.Features.Commerce.ShoppingCart.Queries;

namespace decorativeplant_be.Application.Features.Commerce.ShoppingCart.Handlers;

public class AddToCartHandler : IRequestHandler<AddToCartCommand, ShoppingCartResponse>
{
    private readonly IApplicationDbContext _context;

    public AddToCartHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShoppingCartResponse> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        var listing = await _context.ProductListings.FindAsync(new object[] { cmd.Request.ListingId }, ct)
            ?? throw new NotFoundException($"Listing {cmd.Request.ListingId} not found.");

        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == cmd.UserId, ct);
        var items = new List<CartItemDto>();

        if (cart != null && cart.Items != null)
        {
            items = DeserializeItems(cart.Items);
        }

        var existing = items.FirstOrDefault(i => i.ListingId == cmd.Request.ListingId);
        if (existing != null)
        {
            existing.Quantity += cmd.Request.Quantity;
        }
        else
        {
            items.Add(new CartItemDto
            {
                ListingId = cmd.Request.ListingId,
                Quantity = cmd.Request.Quantity,
                AddedAt = DateTime.UtcNow
            });
        }

        if (cart == null)
        {
            cart = new Domain.Entities.ShoppingCart
            {
                Id = Guid.NewGuid(),
                UserId = cmd.UserId
            };
            _context.ShoppingCarts.Add(cart);
        }

        cart.Items = SerializeItems(items);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // Enrich items with product details before returning
        await EnrichItems(items, _context, ct);

        return MapToResponse(cart, items);
    }

    internal static List<CartItemDto> DeserializeItems(JsonDocument doc)
    {
        var items = new List<CartItemDto>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return items;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            items.Add(new CartItemDto
            {
                ListingId = el.TryGetProperty("listing_id", out var lid) ? Guid.Parse(lid.GetString()!) : Guid.Empty,
                Quantity = el.TryGetProperty("quantity", out var q) ? q.GetInt32() : 0,
                AddedAt = el.TryGetProperty("added_at", out var a) ? DateTime.Parse(a.GetString()!) : null
            });
        }
        return items;
    }

    internal static JsonDocument SerializeItems(List<CartItemDto> items)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(items.Select(i => new
        {
            listing_id = i.ListingId.ToString(),
            quantity = i.Quantity,
            added_at = (i.AddedAt ?? DateTime.UtcNow).ToString("O")
        })));
    }

    internal static ShoppingCartResponse MapToResponse(Domain.Entities.ShoppingCart cart, List<CartItemDto> items)
    {
        return new ShoppingCartResponse
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = items,
            UpdatedAt = cart.UpdatedAt
        };
    }

    internal static async Task EnrichItems(List<CartItemDto> items, IApplicationDbContext context, CancellationToken ct)
    {
        if (items.Count == 0) return;

        var listingIds = items.Select(i => i.ListingId).ToList();
        var listings = await context.ProductListings
            .Include(l => l.Branch)
            .Where(l => listingIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);

        foreach (var item in items)
        {
            if (listings.TryGetValue(item.ListingId, out var listing))
            {
                if (listing.ProductInfo != null)
                {
                    var root = listing.ProductInfo.RootElement;
                    item.Name = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                    if (decimal.TryParse(root.TryGetProperty("price", out var p) ? p.GetString() : "0", out var parsedPrice))
                    {
                        item.Price = parsedPrice;
                    }
                }

                if (listing.Images?.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var firstIdx = listing.Images.RootElement.EnumerateArray().FirstOrDefault();
                    if (firstIdx.ValueKind == JsonValueKind.Object)
                    {
                        item.Image = firstIdx.TryGetProperty("url", out var u) ? u.GetString() : null;
                    }
                }
                item.SellerName = listing.Branch?.Name ?? "Store";
            }
        }
    }
}

public class UpdateCartItemHandler : IRequestHandler<UpdateCartItemCommand, ShoppingCartResponse>
{
    private readonly IApplicationDbContext _context;

    public UpdateCartItemHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShoppingCartResponse> Handle(UpdateCartItemCommand cmd, CancellationToken ct)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == cmd.UserId, ct)
            ?? throw new NotFoundException("Cart not found.");

        var items = cart.Items != null ? AddToCartHandler.DeserializeItems(cart.Items) : new();
        var item = items.FirstOrDefault(i => i.ListingId == cmd.ListingId)
            ?? throw new NotFoundException("Item not in cart.");

        item.Quantity = cmd.Request.Quantity;
        if (item.Quantity <= 0) items.Remove(item);

        cart.Items = AddToCartHandler.SerializeItems(items);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return AddToCartHandler.MapToResponse(cart, items);
    }
}

public class RemoveCartItemHandler : IRequestHandler<RemoveCartItemCommand, ShoppingCartResponse>
{
    private readonly IApplicationDbContext _context;

    public RemoveCartItemHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShoppingCartResponse> Handle(RemoveCartItemCommand cmd, CancellationToken ct)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == cmd.UserId, ct)
            ?? throw new NotFoundException("Cart not found.");

        var items = cart.Items != null ? AddToCartHandler.DeserializeItems(cart.Items) : new();
        items.RemoveAll(i => i.ListingId == cmd.ListingId);

        cart.Items = AddToCartHandler.SerializeItems(items);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return AddToCartHandler.MapToResponse(cart, items);
    }
}

public class ClearCartHandler : IRequestHandler<ClearCartCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ClearCartHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(ClearCartCommand cmd, CancellationToken ct)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == cmd.UserId, ct);
        if (cart == null) return true;

        cart.Items = JsonDocument.Parse("[]");
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }
}

public class GetCartHandler : IRequestHandler<GetCartQuery, ShoppingCartResponse>
{
    private readonly IApplicationDbContext _context;

    public GetCartHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShoppingCartResponse> Handle(GetCartQuery query, CancellationToken ct)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == query.UserId, ct);
        if (cart == null)
            return new ShoppingCartResponse { UserId = query.UserId };

        var items = cart.Items != null ? AddToCartHandler.DeserializeItems(cart.Items) : new();
        
        // Enrich item data with ProductListings and Branches
        await AddToCartHandler.EnrichItems(items, _context, ct);

        return AddToCartHandler.MapToResponse(cart, items);
    }
}
