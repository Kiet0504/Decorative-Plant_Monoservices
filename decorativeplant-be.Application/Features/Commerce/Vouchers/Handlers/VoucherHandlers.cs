using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Vouchers.Commands;
using decorativeplant_be.Application.Features.Commerce.Vouchers.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Vouchers.Handlers;

public class CreateVoucherHandler : IRequestHandler<CreateVoucherCommand, VoucherResponse>
{
    private readonly IApplicationDbContext _context;
    public CreateVoucherHandler(IApplicationDbContext context) => _context = context;

    public async Task<VoucherResponse> Handle(CreateVoucherCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var entity = new Voucher
        {
            Id = Guid.NewGuid(), BranchId = req.BranchId, Code = req.Code, IsActive = true,
            Info = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                name = req.Name, description = req.Description,
                discount_type = req.Type, discount_value = req.Value,
                start_date = req.ValidFrom, end_date = req.ValidTo
            })),
            Rules = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                min_order_amount = req.MinOrder, usage_limit = req.UsageLimits,
                applicable_products = req.ApplicableProducts, used_count = 0
            }))
        };
        _context.Vouchers.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static VoucherResponse MapToResponse(Voucher e)
    {
        var r = new VoucherResponse { Id = e.Id, BranchId = e.BranchId, Code = e.Code, IsActive = e.IsActive };
        if (e.Info != null)
        {
            var root = e.Info.RootElement;
            r.Name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            r.Description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            r.Type = root.TryGetProperty("discount_type", out var dt) ? dt.GetString() : null;
            r.Value = root.TryGetProperty("discount_value", out var dv) ? dv.GetString() : null;
            r.ValidFrom = root.TryGetProperty("start_date", out var sd) ? sd.GetString() : null;
            r.ValidTo = root.TryGetProperty("end_date", out var ed) ? ed.GetString() : null;
        }
        if (e.Rules != null)
        {
            var root = e.Rules.RootElement;
            r.MinOrder = root.TryGetProperty("min_order_amount", out var mo) ? mo.GetString() : null;
            r.UsageLimits = root.TryGetProperty("usage_limit", out var ul) ? ul.GetInt32() : null;
        }
        return r;
    }
}

public class UpdateVoucherHandler : IRequestHandler<UpdateVoucherCommand, VoucherResponse>
{
    private readonly IApplicationDbContext _context;
    public UpdateVoucherHandler(IApplicationDbContext context) => _context = context;
    public async Task<VoucherResponse> Handle(UpdateVoucherCommand cmd, CancellationToken ct)
    {
        var entity = await _context.Vouchers.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Voucher {cmd.Id} not found.");
        var req = cmd.Request;
        if (req.IsActive.HasValue) entity.IsActive = req.IsActive.Value;

        var info = new Dictionary<string, object?>();
        if (entity.Info != null) foreach (var p in entity.Info.RootElement.EnumerateObject()) info[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
        if (req.Name != null) info["name"] = req.Name;
        if (req.Description != null) info["description"] = req.Description;
        if (req.Type != null) info["discount_type"] = req.Type;
        if (req.Value != null) info["discount_value"] = req.Value;
        if (req.ValidFrom != null) info["start_date"] = req.ValidFrom;
        if (req.ValidTo != null) info["end_date"] = req.ValidTo;
        entity.Info = JsonDocument.Parse(JsonSerializer.Serialize(info));

        var rules = new Dictionary<string, object?>();
        if (entity.Rules != null) foreach (var p in entity.Rules.RootElement.EnumerateObject()) rules[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
        if (req.MinOrder != null) rules["min_order_amount"] = req.MinOrder;
        if (req.UsageLimits.HasValue) rules["usage_limit"] = req.UsageLimits.Value;
        entity.Rules = JsonDocument.Parse(JsonSerializer.Serialize(rules));

        await _context.SaveChangesAsync(ct);
        return CreateVoucherHandler.MapToResponse(entity);
    }
}

public class DeleteVoucherHandler : IRequestHandler<DeleteVoucherCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public DeleteVoucherHandler(IApplicationDbContext context) => _context = context;
    public async Task<bool> Handle(DeleteVoucherCommand cmd, CancellationToken ct)
    {
        var e = await _context.Vouchers.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Voucher {cmd.Id} not found.");
        _context.Vouchers.Remove(e); await _context.SaveChangesAsync(ct); return true;
    }
}

public class GetVouchersHandler : IRequestHandler<GetVouchersQuery, PagedResult<VoucherResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetVouchersHandler(IApplicationDbContext context) => _context = context;
    public async Task<PagedResult<VoucherResponse>> Handle(GetVouchersQuery q, CancellationToken ct)
    {
        var query = _context.Vouchers.AsQueryable();
        if (q.BranchId.HasValue) query = query.Where(v => v.BranchId == q.BranchId);
        if (q.ActiveOnly == true) query = query.Where(v => v.IsActive);
        
        var total = await query.CountAsync(ct);
        
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);
            
        return new PagedResult<VoucherResponse>
        {
            Items = items.Select(CreateVoucherHandler.MapToResponse).ToList(),
            TotalCount = total,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }
}

public class GetVoucherByIdHandler : IRequestHandler<GetVoucherByIdQuery, VoucherResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetVoucherByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<VoucherResponse?> Handle(GetVoucherByIdQuery q, CancellationToken ct)
    {
        var e = await _context.Vouchers.FindAsync(new object[] { q.Id }, ct);
        return e == null ? null : CreateVoucherHandler.MapToResponse(e);
    }
}

public class ValidateVoucherHandler : IRequestHandler<ValidateVoucherQuery, ValidateVoucherResponse>
{
    private readonly IApplicationDbContext _context;
    public ValidateVoucherHandler(IApplicationDbContext context) => _context = context;
    public async Task<ValidateVoucherResponse> Handle(ValidateVoucherQuery q, CancellationToken ct)
    {
        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == q.Code, ct);
        if (voucher == null) return new ValidateVoucherResponse { IsValid = false, Message = "Voucher not found." };
        if (!voucher.IsActive) return new ValidateVoucherResponse { IsValid = false, Message = "Voucher is inactive." };
        if (q.BranchId.HasValue && voucher.BranchId != q.BranchId) return new ValidateVoucherResponse { IsValid = false, Message = "Voucher not valid for this branch." };
        return new ValidateVoucherResponse { IsValid = true, Message = "Valid", Voucher = CreateVoucherHandler.MapToResponse(voucher) };
    }
}
