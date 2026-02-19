using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Promotions.Commands;
using decorativeplant_be.Application.Features.Commerce.Promotions.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Promotions.Handlers;

public class CreatePromotionHandler : IRequestHandler<CreatePromotionCommand, PromotionResponse>
{
    private readonly IApplicationDbContext _context;
    public CreatePromotionHandler(IApplicationDbContext context) => _context = context;

    public async Task<PromotionResponse> Handle(CreatePromotionCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var entity = new Promotion
        {
            Id = Guid.NewGuid(), Name = req.Name, BranchId = req.BranchId,
            Config = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                discount_type = req.DiscountType, value = req.Value,
                start_date = req.StartDate, end_date = req.EndDate,
                apply_to_all = req.ApplyToAll, target_categories = req.TargetCategories,
                min_order = req.MinOrder
            }))
        };
        _context.Promotions.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static PromotionResponse MapToResponse(Promotion e)
    {
        var r = new PromotionResponse { Id = e.Id, Name = e.Name, BranchId = e.BranchId };
        if (e.Config != null)
        {
            var root = e.Config.RootElement;
            r.DiscountType = root.TryGetProperty("discount_type", out var dt) ? dt.GetString() : null;
            r.Value = root.TryGetProperty("value", out var v) ? v.GetString() : null;
            r.StartDate = root.TryGetProperty("start_date", out var sd) ? sd.GetString() : null;
            r.EndDate = root.TryGetProperty("end_date", out var ed) ? ed.GetString() : null;
            r.ApplyToAll = root.TryGetProperty("apply_to_all", out var ata) && ata.GetBoolean();
            r.MinOrder = root.TryGetProperty("min_order", out var mo) ? mo.GetString() : null;
            if (root.TryGetProperty("target_categories", out var tc) && tc.ValueKind == JsonValueKind.Array)
                r.TargetCategories = tc.EnumerateArray().Select(x => Guid.Parse(x.GetString()!)).ToList();
        }
        return r;
    }
}

public class UpdatePromotionHandler : IRequestHandler<UpdatePromotionCommand, PromotionResponse>
{
    private readonly IApplicationDbContext _context;
    public UpdatePromotionHandler(IApplicationDbContext context) => _context = context;
    public async Task<PromotionResponse> Handle(UpdatePromotionCommand cmd, CancellationToken ct)
    {
        var entity = await _context.Promotions.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Promotion {cmd.Id} not found.");
        var req = cmd.Request;
        if (req.Name != null) entity.Name = req.Name;
        var config = new Dictionary<string, object?>();
        if (entity.Config != null) foreach (var p in entity.Config.RootElement.EnumerateObject()) config[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
        if (req.DiscountType != null) config["discount_type"] = req.DiscountType;
        if (req.Value != null) config["value"] = req.Value;
        if (req.StartDate != null) config["start_date"] = req.StartDate;
        if (req.EndDate != null) config["end_date"] = req.EndDate;
        if (req.ApplyToAll.HasValue) config["apply_to_all"] = req.ApplyToAll.Value;
        if (req.MinOrder != null) config["min_order"] = req.MinOrder;
        entity.Config = JsonDocument.Parse(JsonSerializer.Serialize(config));
        await _context.SaveChangesAsync(ct);
        return CreatePromotionHandler.MapToResponse(entity);
    }
}

public class DeletePromotionHandler : IRequestHandler<DeletePromotionCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public DeletePromotionHandler(IApplicationDbContext context) => _context = context;
    public async Task<bool> Handle(DeletePromotionCommand cmd, CancellationToken ct)
    {
        var e = await _context.Promotions.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Promotion {cmd.Id} not found.");
        _context.Promotions.Remove(e); await _context.SaveChangesAsync(ct); return true;
    }
}

public class GetPromotionsHandler : IRequestHandler<GetPromotionsQuery, List<PromotionResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetPromotionsHandler(IApplicationDbContext context) => _context = context;
    public async Task<List<PromotionResponse>> Handle(GetPromotionsQuery q, CancellationToken ct)
    {
        var query = _context.Promotions.AsQueryable();
        if (q.BranchId.HasValue) query = query.Where(p => p.BranchId == q.BranchId);
        return (await query.ToListAsync(ct)).Select(CreatePromotionHandler.MapToResponse).ToList();
    }
}

public class GetPromotionByIdHandler : IRequestHandler<GetPromotionByIdQuery, PromotionResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetPromotionByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<PromotionResponse?> Handle(GetPromotionByIdQuery q, CancellationToken ct)
    {
        var e = await _context.Promotions.FindAsync(new object[] { q.Id }, ct);
        return e == null ? null : CreatePromotionHandler.MapToResponse(e);
    }
}
