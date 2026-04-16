using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders;
using decorativeplant_be.Application.Features.Commerce.Returns.Commands;
using decorativeplant_be.Application.Features.Commerce.Returns.Queries;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Returns.Handlers;

public class CreateReturnRequestHandler : IRequestHandler<CreateReturnRequestCommand, ReturnRequestResponse>
{
    private readonly IApplicationDbContext _context;
    public CreateReturnRequestHandler(IApplicationDbContext context) => _context = context;

    public async Task<ReturnRequestResponse> Handle(CreateReturnRequestCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new BadRequestException("Reason is required.");

        var order = await _context.OrderHeaders.FirstOrDefaultAsync(o => o.Id == req.OrderId, ct)
            ?? throw new NotFoundException($"Order {req.OrderId} not found.");

        if (order.UserId != cmd.UserId)
            throw new BadRequestException("Order does not belong to the authenticated user.");

        if (order.Status != OrderStatusMachine.Delivered && order.Status != OrderStatusMachine.Completed)
            throw new BadRequestException("Order must be delivered or completed to request a return.");

        var existing = await _context.ReturnRequests
            .AnyAsync(r => r.OrderId == req.OrderId && r.Status != "rejected", ct);
        if (existing)
            throw new BadRequestException("A return request already exists for this order.");

        var entity = new ReturnRequest
        {
            Id = Guid.NewGuid(),
            OrderId = req.OrderId,
            UserId = cmd.UserId,
            Status = "pending",
            Info = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                reason = req.Reason,
                description = req.Description,
            })),
            Images = req.Images?.Count > 0
                ? JsonDocument.Parse(JsonSerializer.Serialize(
                    req.Images.Select(i => new { url = i.Url, alt = i.Alt, sort = i.Sort })))
                : null,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ReturnRequests.Add(entity);
        await _context.SaveChangesAsync(ct);

        return MapToResponse(entity, order.OrderCode);
    }

    internal static ReturnRequestResponse MapToResponse(ReturnRequest e, string? orderCode = null)
    {
        var r = new ReturnRequestResponse
        {
            Id = e.Id,
            OrderId = e.OrderId,
            OrderCode = orderCode,
            UserId = e.UserId,
            Status = e.Status ?? "pending",
            CreatedAt = e.CreatedAt,
        };

        if (e.Info != null)
        {
            var root = e.Info.RootElement;
            r.Reason = root.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
            r.Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            r.ResolutionNote = root.TryGetProperty("resolution_note", out var note) ? note.GetString() : null;
            if (root.TryGetProperty("resolved_at", out var ra) && DateTime.TryParse(ra.GetString(), out var dt))
                r.ResolvedAt = dt;
        }

        if (e.Images?.RootElement.ValueKind == JsonValueKind.Array)
            r.Images = e.Images.RootElement.EnumerateArray().Select(img => new ReturnImageDto
            {
                Url = img.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Alt = img.TryGetProperty("alt", out var a) ? a.GetString() : null,
                Sort = img.TryGetProperty("sort", out var s) ? s.GetInt32() : 0,
            }).ToList();

        return r;
    }
}

public class UpdateReturnStatusHandler : IRequestHandler<UpdateReturnStatusCommand, ReturnRequestResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockService _stockService;
    public UpdateReturnStatusHandler(IApplicationDbContext context, IStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public async Task<ReturnRequestResponse> Handle(UpdateReturnStatusCommand cmd, CancellationToken ct)
    {
        var status = (cmd.Request.Status ?? "").ToLowerInvariant();
        if (status is not ("pending" or "approved" or "rejected" or "refunded"))
            throw new BadRequestException($"Invalid return status '{cmd.Request.Status}'.");

        var entity = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"Return request {cmd.Id} not found.");

        var order = entity.OrderId.HasValue
            ? await _context.OrderHeaders.Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == entity.OrderId.Value, ct)
            : null;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                entity.Status = status;

                var info = new Dictionary<string, object?>();
                if (entity.Info != null)
                {
                    foreach (var p in entity.Info.RootElement.EnumerateObject())
                    {
                        info[p.Name] = p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString()
                            : JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
                    }
                }
                if (!string.IsNullOrWhiteSpace(cmd.Request.ResolutionNote))
                    info["resolution_note"] = cmd.Request.ResolutionNote;
                info["resolved_at"] = DateTime.UtcNow.ToString("o");
                info["resolved_by"] = cmd.ActorUserId?.ToString();
                entity.Info = JsonDocument.Parse(JsonSerializer.Serialize(info));

                // On approval: transition order to returned + restore stock.
                if (status == "approved" && order != null)
                {
                    OrderStatusMachine.ApplyFromExternalSource(
                        order,
                        OrderStatusMachine.Returned,
                        source: "ReturnRequestApproved",
                        reason: cmd.Request.ResolutionNote ?? "Return approved");

                    if (order.OrderItems != null)
                        await _stockService.RestoreOrderStockAsync(order.OrderItems, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        return CreateReturnRequestHandler.MapToResponse(entity, order?.OrderCode);
    }
}

public class GetReturnByIdHandler : IRequestHandler<GetReturnByIdQuery, ReturnRequestResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetReturnByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<ReturnRequestResponse?> Handle(GetReturnByIdQuery q, CancellationToken ct)
    {
        var e = await _context.ReturnRequests
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == q.Id, ct);
        return e == null ? null : CreateReturnRequestHandler.MapToResponse(e, e.Order?.OrderCode);
    }
}

public class GetMyReturnsHandler : IRequestHandler<GetMyReturnsQuery, PagedResult<ReturnRequestResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetMyReturnsHandler(IApplicationDbContext context) => _context = context;
    public async Task<PagedResult<ReturnRequestResponse>> Handle(GetMyReturnsQuery q, CancellationToken ct)
    {
        var query = _context.ReturnRequests
            .Include(r => r.Order)
            .Where(r => r.UserId == q.UserId);
        var total = await query.CountAsync(ct);
        var list = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<ReturnRequestResponse>
        {
            Items = list.Select(e => CreateReturnRequestHandler.MapToResponse(e, e.Order?.OrderCode)).ToList(),
            TotalCount = total,
            Page = q.Page,
            PageSize = q.PageSize,
        };
    }
}

public class GetAllReturnsHandler : IRequestHandler<GetAllReturnsQuery, PagedResult<ReturnRequestResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetAllReturnsHandler(IApplicationDbContext context) => _context = context;
    public async Task<PagedResult<ReturnRequestResponse>> Handle(GetAllReturnsQuery q, CancellationToken ct)
    {
        var query = _context.ReturnRequests.Include(r => r.Order).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);
        var total = await query.CountAsync(ct);
        var list = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<ReturnRequestResponse>
        {
            Items = list.Select(e => CreateReturnRequestHandler.MapToResponse(e, e.Order?.OrderCode)).ToList(),
            TotalCount = total,
            Page = q.Page,
            PageSize = q.PageSize,
        };
    }
}
