using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.Branch;
using decorativeplant_be.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.API.Controllers;

[Route("api/staff/branches")]
[ApiController]
public class BranchController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public BranchController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BranchDto>>> GetBranches()
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .ToListAsync();

        return Ok(branches.Select(b => new BranchDto
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            Code = b.Code,
            Name = b.Name,
            Slug = b.Slug,
            BranchType = b.BranchType,
            ContactInfo = b.ContactInfo,
            OperatingHours = b.OperatingHours,
            Settings = b.Settings,
            IsActive = b.IsActive
        }));
    }

    [HttpPost]
    public async Task<ActionResult<BranchDto>> CreateBranch([FromBody] CreateBranchDto dto)
    {
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = dto.CompanyId == Guid.Empty ? Guid.NewGuid() : dto.CompanyId, // Default company if empty
            Code = dto.Code,
            Name = dto.Name,
            Slug = dto.Slug,
            BranchType = dto.BranchType,
            ContactInfo = dto.ContactInfo,
            OperatingHours = dto.OperatingHours,
            Settings = dto.Settings,
            IsActive = dto.IsActive
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(CancellationToken.None);

        var response = new BranchDto
        {
            Id = branch.Id,
            CompanyId = branch.CompanyId,
            Code = branch.Code,
            Name = branch.Name,
            Slug = branch.Slug,
            BranchType = branch.BranchType,
            ContactInfo = branch.ContactInfo,
            OperatingHours = branch.OperatingHours,
            Settings = branch.Settings,
            IsActive = branch.IsActive
        };

        return CreatedAtAction(nameof(GetBranches), new { id = branch.Id }, response);
    }
}
