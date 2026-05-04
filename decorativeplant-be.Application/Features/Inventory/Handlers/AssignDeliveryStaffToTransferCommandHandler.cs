using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Options;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class AssignDeliveryStaffToTransferCommandHandler : IRequestHandler<AssignDeliveryStaffToTransferCommand, StockTransferDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly CustomerPortalLinksOptions _portalOptions;
    private readonly ILogger<AssignDeliveryStaffToTransferCommandHandler> _logger;

    public AssignDeliveryStaffToTransferCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IOptions<CustomerPortalLinksOptions> portalOptions,
        ILogger<AssignDeliveryStaffToTransferCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _portalOptions = portalOptions.Value;
        _logger = logger;
    }

    public async Task<StockTransferDto> Handle(AssignDeliveryStaffToTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .FirstOrDefaultAsync(t => t.Id == request.TransferId, cancellationToken);

        if (transfer == null)
            throw new NotFoundException(nameof(StockTransfer), request.TransferId);

        var statusNorm = transfer.Status ?? "";
        if (!statusNorm.Equals("approved", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Transfer must be Approved before assigning delivery staff.");

        if (!transfer.FromBranchId.HasValue)
            throw new ValidationException("Transfer has no source branch.");

        if (request.StaffId == Guid.Empty)
            throw new ValidationException("Select exactly one fulfillment staff member to dispatch this transfer.");

        var role = request.ActingRole?.Trim().ToLowerInvariant();
        var isAdmin = role == "admin";
        if (!isAdmin)
        {
            if (!request.ActingUserId.HasValue)
                throw new ValidationException("User context is required to assign delivery staff.");

            var managerAssignment = await _context.StaffAssignments
                .AsNoTracking()
                .Include(sa => sa.Staff)
                .FirstOrDefaultAsync(
                    sa => sa.StaffId == request.ActingUserId.Value
                          && sa.BranchId == transfer.FromBranchId.Value,
                    cancellationToken);

            if (managerAssignment?.Staff.Role?.Equals("branch_manager", StringComparison.OrdinalIgnoreCase) != true)
                throw new ValidationException("Only a branch manager of the originating branch (or admin) can assign delivery staff.");
        }

        var branchAssignments = await _context.StaffAssignments
            .Include(sa => sa.Staff)
            .Where(sa => sa.BranchId == transfer.FromBranchId.Value)
            .ToListAsync(cancellationToken);

        var fulfillmentAtBranchIds = branchAssignments
            .Where(sa => sa.Staff != null
                         && string.Equals(sa.Staff.Role, "fulfillment_staff", StringComparison.OrdinalIgnoreCase))
            .Select(sa => sa.StaffId)
            .Distinct()
            .ToList();

        if (fulfillmentAtBranchIds.Count < 2)
            throw new ValidationException(
                "The originating branch must have at least two fulfillment staff accounts for pack-and-ship operations.");

        // Avoid EF translators that fail on PostgreSQL uuid[] / string.Equals OrdinalIgnoreCase in SQL:
        // load candidates, then evaluate busy dispatcher IDs in memory.
        var otherPendingOutbound = await _context.StockTransfers
            .AsNoTracking()
            .Where(t =>
                t.FromBranchId == transfer.FromBranchId
                && t.Id != transfer.Id
                && t.Status != null
                && t.Status == "delivery_staff_assigned")
            .ToListAsync(cancellationToken);

        var busyIds = otherPendingOutbound
            .Where(t => t.DeliveryStaffIds is { Count: > 0 })
            .SelectMany(t => t.DeliveryStaffIds!)
            .ToHashSet();
        var availableIds = fulfillmentAtBranchIds.Where(id => !busyIds.Contains(id)).ToList();

        if (availableIds.Count < 2)
            throw new ValidationException(
                "At least two fulfillment staff must be available (not already assigned as the dispatcher on another transfer awaiting shipment). Assigning the only free person would leave no one available to support packing.");

        var validFulfillmentIds = fulfillmentAtBranchIds.ToHashSet();
        if (!validFulfillmentIds.Contains(request.StaffId))
            throw new ValidationException(
                "Selected user must be a fulfillment staff member assigned to the originating branch.");

        if (!availableIds.Contains(request.StaffId))
            throw new ValidationException(
                "Choose an available fulfillment colleague. This staff member is already assigned to another outbound transfer that has not been shipped.");

        transfer.DeliveryStaffIds = new List<Guid> { request.StaffId };
        transfer.Status = "delivery_staff_assigned";

        transfer.LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
            existingInfo: transfer.LogisticsInfo,
            deliveryStaffAssignedAtUtc: DateTime.UtcNow,
            deliveryStaffAssignedByName: request.AssignedByName);

        await _context.SaveChangesAsync(cancellationToken);

        var recipient = await _context.UserAccounts
            .Where(u => u.Id == request.StaffId)
            .Select(u => new { u.Email, u.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);

        var notifyList = recipient != null && !string.IsNullOrWhiteSpace(recipient.Email)
            ? new List<(string Email, string? DisplayName)> { (recipient.Email, recipient.DisplayName) }
            : new List<(string Email, string? DisplayName)>();

        await StockTransferDeliveryAssignmentNotifier.TryNotifyAssignedFulfillmentStaffAsync(
            _emailService,
            _logger,
            _portalOptions,
            notifyList,
            transfer.TransferCode ?? transfer.Id.ToString()[..8].ToUpperInvariant(),
            InventoryMapper.GetSpeciesDisplayName(transfer.Batch),
            transfer.ToBranch?.Name,
            transfer.Quantity,
            cancellationToken);

        return InventoryMapper.ToStockTransferDto(transfer);
    }
}
