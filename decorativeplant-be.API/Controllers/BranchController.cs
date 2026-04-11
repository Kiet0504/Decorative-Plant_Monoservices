// decorativeplant-be.API/Controllers/BranchController.cs

using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Branch.Commands;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[Route("api/branches")]
[Authorize]
public class BranchController : BaseController
{

    [HttpGet("company/{companyId:guid}")]
    public async Task<ActionResult<List<BranchDto>>> GetByCompany(Guid companyId, [FromQuery] bool? onlyActive)
    {
        try
        {
            var result = await Mediator.Send(new GetBranchesByCompanyQuery
            {
                CompanyId = companyId,
                OnlyActive = onlyActive
            });
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchDto>> GetById(Guid id)
    {
        try
        {
            var result = await Mediator.Send(new GetBranchByIdQuery { Id = id });
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpGet("{branchId:guid}/staff")]
    public async Task<ActionResult<List<StaffAssignmentDto>>> GetStaffByBranch(Guid branchId)
    {
        try
        {
            var result = await Mediator.Send(new GetStaffAssignmentsByBranchQuery { BranchId = branchId });
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpGet("staff/{staffId:guid}")]
    public async Task<ActionResult<List<BranchDto>>> GetByStaff(Guid staffId)
    {
        try
        {
            var result = await Mediator.Send(new GetBranchesByStaffQuery { StaffId = staffId });
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<BranchDto>> Create([FromBody] CreateBranchCommand command)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(Problem(detail: "User ID not found in token", statusCode: 401));
            }

            var updatedCommand = command with { CurrentUserId = userId.Value };
            var result = await Mediator.Send(updatedCommand);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpPut("{branchId:guid}")]
    [Authorize(Roles = "admin,branchManager,branch_manager")]
    public async Task<ActionResult<BranchDto>> Update(Guid branchId, [FromBody] UpdateBranchCommand command)
    {
        try
        {
            var updatedCommand = command with { Id = branchId };
            var result = await Mediator.Send(updatedCommand);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpDelete("{branchId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Deactivate(Guid branchId)
    {
        try
        {
            await Mediator.Send(new DeactivateBranchCommand { Id = branchId });
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpPost("{branchId:guid}/staff")]
    [Authorize(Roles = "admin,branchManager,branch_manager")]
    public async Task<ActionResult<StaffAssignmentDto>> AssignStaff(Guid branchId, [FromBody] AssignStaffToBranchCommand command)
    {
        try
        {
            // Get current user's role and branch from HttpContext (set by BranchScopedAccessMiddleware)
            var currentUserRole = HttpContext.Items["CurrentRole"]?.ToString() ?? string.Empty;
            var currentUserBranchId = HttpContext.Items["CurrentBranchId"] as Guid?;

            var updatedCommand = command with
            {
                BranchId = branchId,
                CurrentUserRole = currentUserRole,
                CurrentUserBranchId = currentUserBranchId
            };
            var result = await Mediator.Send(updatedCommand);
            return CreatedAtAction(nameof(GetStaffByBranch), new { branchId }, result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpPut("{branchId:guid}/staff/{assignmentId:guid}")]
    [Authorize(Roles = "admin,branchManager,branch_manager")]
    public async Task<ActionResult<StaffAssignmentDto>> UpdateStaffAssignment(Guid branchId, Guid assignmentId, [FromBody] UpdateStaffAssignmentCommand command)
    {
        try
        {
            // Get current user's role and branch from HttpContext (set by BranchScopedAccessMiddleware)
            var currentUserRole = HttpContext.Items["CurrentRole"]?.ToString() ?? string.Empty;
            var currentUserBranchId = HttpContext.Items["CurrentBranchId"] as Guid?;

            var updatedCommand = command with
            {
                StaffAssignmentId = assignmentId,
                CurrentUserRole = currentUserRole,
                CurrentUserBranchId = currentUserBranchId
            };
            var result = await Mediator.Send(updatedCommand);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpDelete("{branchId:guid}/staff/{assignmentId:guid}")]
    [Authorize(Roles = "admin,branchManager,branch_manager")]
    public async Task<ActionResult> UnassignStaff(Guid branchId, Guid assignmentId)
    {
        try
        {
            // Get current user's role and ID from HttpContext (set by BranchScopedAccessMiddleware)
            var currentUserRole = HttpContext.Items["CurrentRole"]?.ToString() ?? string.Empty;
            var currentUserId = HttpContext.Items["CurrentUserId"] as Guid?;

            await Mediator.Send(new UnassignStaffFromBranchCommand
            {
                StaffAssignmentId = assignmentId,
                CurrentUserRole = currentUserRole,
                CurrentUserId = currentUserId
            });
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }
}
