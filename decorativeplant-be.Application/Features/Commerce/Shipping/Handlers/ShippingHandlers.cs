using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Shipping.Commands;
using decorativeplant_be.Application.Features.Commerce.Shipping.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Shipping.Handlers;

public class CreateShippingHandler : IRequestHandler<CreateShippingCommand, ShippingResponse>
{
    private readonly IApplicationDbContext _context;
    public CreateShippingHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShippingResponse> Handle(CreateShippingCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var entity = new Domain.Entities.Shipping
        {
            Id = Guid.NewGuid(),
            OrderId = req.OrderId,
            TrackingCode = $"TRK-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            CarrierInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { carrier = req.Carrier, method = req.Method, fee = req.Fee })),
            Status = "pending",
            Events = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new { status = "pending", location = "", description = "Shipping created", event_time = DateTime.UtcNow.ToString("O") } }))
        };
        _context.Shippings.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static ShippingResponse MapToResponse(Domain.Entities.Shipping e)
    {
        var r = new ShippingResponse { Id = e.Id, OrderId = e.OrderId, TrackingCode = e.TrackingCode, Status = e.Status };
        if (e.CarrierInfo != null)
        {
            var root = e.CarrierInfo.RootElement;
            r.Carrier = root.TryGetProperty("carrier", out var c) ? c.GetString() : null;
            r.Method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
            r.Fee = root.TryGetProperty("fee", out var f) ? f.GetString() : null;
        }
        if (e.DeliveryDetails != null)
        {
            var root = e.DeliveryDetails.RootElement;
            if (root.TryGetProperty("dates", out var dates))
            {
                r.EstimatedDate = dates.TryGetProperty("estimated", out var est) ? est.GetString() : null;
                r.ActualDate = dates.TryGetProperty("actual", out var act) ? act.GetString() : null;
            }
        }
        if (e.Events != null && e.Events.RootElement.ValueKind == JsonValueKind.Array)
        {
            r.Events = e.Events.RootElement.EnumerateArray().Select(ev => new ShippingEventDto
            {
                Status = ev.TryGetProperty("status", out var s) ? s.GetString() : null,
                Location = ev.TryGetProperty("location", out var l) ? l.GetString() : null,
                Description = ev.TryGetProperty("description", out var d) ? d.GetString() : null,
                EventTime = ev.TryGetProperty("event_time", out var t) ? t.GetString() : null
            }).ToList();
        }
        return r;
    }
}

public class UpdateShippingStatusHandler : IRequestHandler<UpdateShippingStatusCommand, ShippingResponse>
{
    private readonly IApplicationDbContext _context;
    public UpdateShippingStatusHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShippingResponse> Handle(UpdateShippingStatusCommand cmd, CancellationToken ct)
    {
        var entity = await _context.Shippings.FindAsync(new object[] { cmd.Id }, ct)
            ?? throw new NotFoundException($"Shipping {cmd.Id} not found.");
        entity.Status = cmd.Request.Status;

        var events = new List<object>();
        if (entity.Events?.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var ev in entity.Events.RootElement.EnumerateArray())
                events.Add(JsonSerializer.Deserialize<object>(ev.GetRawText())!);

        events.Add(new { status = cmd.Request.Status, location = cmd.Request.Location ?? "", description = cmd.Request.Description ?? "", event_time = DateTime.UtcNow.ToString("O") });
        entity.Events = JsonDocument.Parse(JsonSerializer.Serialize(events));

        await _context.SaveChangesAsync(ct);
        return CreateShippingHandler.MapToResponse(entity);
    }
}

public class GetShippingByOrderHandler : IRequestHandler<GetShippingByOrderQuery, List<ShippingResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetShippingByOrderHandler(IApplicationDbContext context) => _context = context;
    public async Task<List<ShippingResponse>> Handle(GetShippingByOrderQuery q, CancellationToken ct)
    {
        var list = await _context.Shippings.Where(s => s.OrderId == q.OrderId).ToListAsync(ct);
        return list.Select(CreateShippingHandler.MapToResponse).ToList();
    }
}

public class CreateShippingZoneHandler : IRequestHandler<CreateShippingZoneCommand, ShippingZoneResponse>
{
    private readonly IApplicationDbContext _context;
    public CreateShippingZoneHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShippingZoneResponse> Handle(CreateShippingZoneCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var entity = new ShippingZone
        {
            Id = Guid.NewGuid(), BranchId = req.BranchId, Name = req.Name,
            Locations = JsonDocument.Parse(JsonSerializer.Serialize(new { cities = req.Cities, districts = req.Districts })),
            FeeConfig = JsonDocument.Parse(JsonSerializer.Serialize(new { base_fee = req.BaseFee, fee_per_km = req.FeePerKm, free_threshold = req.FreeThreshold })),
            DeliveryTimeConfig = JsonDocument.Parse(JsonSerializer.Serialize(new { min_days = req.MinDays, max_days = req.MaxDays }))
        };
        _context.ShippingZones.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static ShippingZoneResponse MapToResponse(ShippingZone e)
    {
        var r = new ShippingZoneResponse { Id = e.Id, BranchId = e.BranchId, Name = e.Name };
        if (e.Locations != null)
        {
            var root = e.Locations.RootElement;
            if (root.TryGetProperty("cities", out var c) && c.ValueKind == JsonValueKind.Array) r.Cities = c.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            if (root.TryGetProperty("districts", out var d) && d.ValueKind == JsonValueKind.Array) r.Districts = d.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        }
        if (e.FeeConfig != null)
        {
            var root = e.FeeConfig.RootElement;
            r.BaseFee = root.TryGetProperty("base_fee", out var bf) ? bf.GetString() ?? "0" : "0";
            r.FeePerKm = root.TryGetProperty("fee_per_km", out var fpk) ? fpk.GetString() : null;
            r.FreeThreshold = root.TryGetProperty("free_threshold", out var ft) ? ft.GetString() : null;
        }
        if (e.DeliveryTimeConfig != null)
        {
            var root = e.DeliveryTimeConfig.RootElement;
            r.MinDays = root.TryGetProperty("min_days", out var mn) ? mn.GetInt32() : 1;
            r.MaxDays = root.TryGetProperty("max_days", out var mx) ? mx.GetInt32() : 3;
        }
        return r;
    }
}

public class UpdateShippingZoneHandler : IRequestHandler<UpdateShippingZoneCommand, ShippingZoneResponse>
{
    private readonly IApplicationDbContext _context;
    public UpdateShippingZoneHandler(IApplicationDbContext context) => _context = context;
    public async Task<ShippingZoneResponse> Handle(UpdateShippingZoneCommand cmd, CancellationToken ct)
    {
        var entity = await _context.ShippingZones.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"ShippingZone {cmd.Id} not found.");
        var req = cmd.Request;
        if (req.Name != null) entity.Name = req.Name;
        if (req.Cities != null || req.Districts != null)
            entity.Locations = JsonDocument.Parse(JsonSerializer.Serialize(new { cities = req.Cities ?? new(), districts = req.Districts ?? new() }));
        if (req.BaseFee != null || req.FeePerKm != null || req.FreeThreshold != null)
            entity.FeeConfig = JsonDocument.Parse(JsonSerializer.Serialize(new { base_fee = req.BaseFee ?? "0", fee_per_km = req.FeePerKm, free_threshold = req.FreeThreshold }));
        if (req.MinDays.HasValue || req.MaxDays.HasValue)
            entity.DeliveryTimeConfig = JsonDocument.Parse(JsonSerializer.Serialize(new { min_days = req.MinDays ?? 1, max_days = req.MaxDays ?? 3 }));
        await _context.SaveChangesAsync(ct);
        return CreateShippingZoneHandler.MapToResponse(entity);
    }
}

public class DeleteShippingZoneHandler : IRequestHandler<DeleteShippingZoneCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public DeleteShippingZoneHandler(IApplicationDbContext context) => _context = context;
    public async Task<bool> Handle(DeleteShippingZoneCommand cmd, CancellationToken ct)
    {
        var entity = await _context.ShippingZones.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"ShippingZone {cmd.Id} not found.");
        _context.ShippingZones.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}

public class GetShippingZonesHandler : IRequestHandler<GetShippingZonesQuery, List<ShippingZoneResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetShippingZonesHandler(IApplicationDbContext context) => _context = context;
    public async Task<List<ShippingZoneResponse>> Handle(GetShippingZonesQuery q, CancellationToken ct)
    {
        var query = _context.ShippingZones.AsQueryable();
        if (q.BranchId.HasValue) query = query.Where(z => z.BranchId == q.BranchId);
        return (await query.ToListAsync(ct)).Select(CreateShippingZoneHandler.MapToResponse).ToList();
    }
}

public class GetShippingZoneByIdHandler : IRequestHandler<GetShippingZoneByIdQuery, ShippingZoneResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetShippingZoneByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<ShippingZoneResponse?> Handle(GetShippingZoneByIdQuery q, CancellationToken ct)
    {
        var e = await _context.ShippingZones.FindAsync(new object[] { q.Id }, ct);
        return e == null ? null : CreateShippingZoneHandler.MapToResponse(e);
    }
}
