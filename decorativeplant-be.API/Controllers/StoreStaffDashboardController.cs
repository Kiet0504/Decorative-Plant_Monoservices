using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.StoreStaff.DTOs;
using decorativeplant_be.Application.Features.StoreStaff.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/store-staff")]
[Authorize]
public class StoreStaffDashboardController : BaseController
{
    /// <summary>Aggregated KPIs for the store dashboard (branch-scoped).</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? branchId,
        [FromServices] IApplicationDbContext context)
    {
        var isAdmin = User.IsInRole("admin");
        var isStaff = User.IsInRole("store_staff")
            || User.IsInRole("branch_manager")
            || User.IsInRole("fulfillment_staff");

        if (!isAdmin && !isStaff)
            return StatusCode(403, ApiResponse<object>.ErrorResponse("Forbidden", statusCode: 403));

        Guid? effectiveBranchId = branchId;

        if (isStaff && !effectiveBranchId.HasValue)
        {
            var staffUserId = GetUserId();
            if (!staffUserId.HasValue)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Unauthorized", statusCode: 401));

            effectiveBranchId = await context.StaffAssignments.AsNoTracking()
                .Where(s => s.StaffId == staffUserId && s.IsPrimary)
                .Select(s => (Guid?)s.BranchId)
                .FirstOrDefaultAsync();
        }

        if (!effectiveBranchId.HasValue)
            return BadRequest(ApiResponse<object>.ErrorResponse("branchId is required for this user.", statusCode: 400));

        StoreStaffDashboardDto result = await Mediator.Send(new GetStoreStaffDashboardQuery(effectiveBranchId.Value));
        return Ok(ApiResponse<StoreStaffDashboardDto>.SuccessResponse(result));
    }
}
