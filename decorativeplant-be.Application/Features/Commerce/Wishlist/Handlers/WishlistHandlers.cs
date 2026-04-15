using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Wishlist.Commands;
using decorativeplant_be.Application.Features.Commerce.Wishlist.Queries;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Handlers;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Wishlist.Handlers;

public class AddWishlistItemHandler : IRequestHandler<AddWishlistItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AddWishlistItemHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(AddWishlistItemCommand cmd, CancellationToken ct)
    {
        var listingId = cmd.Request.ListingId;

        var listingExists = await _context.ProductListings.AnyAsync(l => l.Id == listingId, ct);
        if (!listingExists) throw new NotFoundException($"Listing {listingId} not found.");

        var exists = await _context.WishlistItems.AnyAsync(w => w.UserId == cmd.UserId && w.ListingId == listingId, ct);
        if (exists) return true;

        _context.WishlistItems.Add(new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = cmd.UserId,
            ListingId = listingId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
        return true;
    }
}

public class RemoveWishlistItemHandler : IRequestHandler<RemoveWishlistItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveWishlistItemHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(RemoveWishlistItemCommand cmd, CancellationToken ct)
    {
        var entity = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == cmd.UserId && w.ListingId == cmd.ListingId, ct);

        if (entity == null) return false;

        _context.WishlistItems.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}

public class GetWishlistHandler : IRequestHandler<GetWishlistQuery, List<ProductListingResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetWishlistHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ProductListingResponse>> Handle(GetWishlistQuery query, CancellationToken ct)
    {
        var listingIds = await _context.WishlistItems
            .Where(w => w.UserId == query.UserId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => w.ListingId)
            .ToListAsync(ct);

        if (listingIds.Count == 0) return new List<ProductListingResponse>();

        var listings = await _context.ProductListings
            .Include(x => x.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .Include(x => x.Branch)
            .Where(x => listingIds.Contains(x.Id))
            .ToListAsync(ct);

        var mapped = listings
            .Select(CreateProductListingHandler.MapToResponse)
            .ToDictionary(x => x.Id, x => x);

        // preserve wishlist ordering
        var result = new List<ProductListingResponse>(listingIds.Count);
        foreach (var id in listingIds)
        {
            if (mapped.TryGetValue(id, out var dto))
            {
                result.Add(dto);
            }
        }

        return result;
    }
}

