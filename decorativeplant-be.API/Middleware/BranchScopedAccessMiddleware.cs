// decorativeplant-be.API/Middleware/BranchScopedAccessMiddleware.cs

using System.Security.Claims;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.API.Middleware;

public class BranchScopedAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;

    public BranchScopedAccessMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Step 1 — Extract claims
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        var branchClaim = context.User.FindFirst("branch_id")?.Value;

        if (sub == null || role == null)
        {
            await _next(context);
            return;
        }

        context.Items["CurrentUserId"] = Guid.Parse(sub);
        context.Items["CurrentRole"] = role;
        context.Items["CurrentBranchId"] = branchClaim != null ? Guid.Parse(branchClaim) : (Guid?)null;

        var roleNorm = StaffRoleNormalizer.Normalize(role);

        // Step 2 — Admin bypass
        if (roleNorm == "admin")
        {
            await _next(context);
            return;
        }

        // Step 3 — Mutation guard (POST/PUT/PATCH/DELETE)
        if (HttpMethods.IsPost(context.Request.Method) ||
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method) ||
            HttpMethods.IsDelete(context.Request.Method))
        {
            var routeBranchId = TryGetBranchIdFromRoute(context);

            if (routeBranchId.HasValue && routeBranchId != (Guid?)context.Items["CurrentBranchId"])
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "CROSS_BRANCH_MUTATION",
                    message = "Access denied: cross-branch mutation not allowed"
                });
                return;
            }
        }

        // Step 4 — Staff GET guard
        if (HttpMethods.IsGet(context.Request.Method) &&
            roleNorm is "cultivation_staff" or "store_staff" or "fulfillment_staff")
        {
            var routeBranchId = TryGetBranchIdFromRoute(context);

            if (routeBranchId.HasValue)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var assignment = await db.StaffAssignments
                    .FirstOrDefaultAsync(sa =>
                        sa.StaffId == (Guid)context.Items["CurrentUserId"]! &&
                        sa.BranchId == routeBranchId.Value);

                if (assignment == null)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        code = "NO_BRANCH_ACCESS",
                        message = "Not assigned to this branch"
                    });
                    return;
                }

                var canViewOthers = assignment.Permissions?.RootElement
                    .TryGetProperty("can_view_other_branches", out var p) == true && p.GetBoolean();

                if (routeBranchId != (Guid?)context.Items["CurrentBranchId"] && !canViewOthers)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        code = "CROSS_BRANCH_READ",
                        message = "Access denied: cannot view other branches"
                    });
                    return;
                }
            }
        }

        // Step 5: Continue pipeline
        await _next(context);
    }

    private static Guid? TryGetBranchIdFromRoute(HttpContext context)
    {
        // 1. Always trust "branchId" parameter
        if (context.Request.RouteValues.TryGetValue("branchId", out var bVal) &&
            Guid.TryParse(bVal?.ToString(), out var bId))
        {
            return bId;
        }

        // 2. Only treat "id" as a branch ID if the path refers to a branch resource
        // This prevents false-positives on routes like /api/iot/automation-rules/{id}
        if (context.Request.Path.StartsWithSegments("/api/branches") &&
            context.Request.RouteValues.TryGetValue("id", out var idVal) &&
            Guid.TryParse(idVal?.ToString(), out var id))
        {
            return id;
        }

        return null;
    }
}
