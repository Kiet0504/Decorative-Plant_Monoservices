// decorativeplant-be.Application/Features/Branch/Handlers/CreateBranchCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchDto>
{
    private readonly IApplicationDbContext _context;

    public CreateBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        // 1. Get current user and their company
        var currentUser = await _context.UserAccounts
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId, cancellationToken);

        if (currentUser == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.UserAccount), request.CurrentUserId);
        }

        if (currentUser.CompanyId == null)
        {
            throw new InvalidOperationException("Current user is not associated with any company. Please contact system administrator to link your account to a company.");
        }

        var company = currentUser.Company;
        if (company == null)
        {
            // Fallback: Load company explicitly if navigation property is null
            company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == currentUser.CompanyId, cancellationToken);

            if (company == null)
            {
                throw new NotFoundException(nameof(Domain.Entities.Company), currentUser.CompanyId.Value);
            }
        }

        // 2. Auto-generate branch code with retry mechanism to handle race conditions
        Domain.Entities.Branch? branch = null;
        int retryCount = 0;
        const int maxRetries = 3;
        bool success = false;

        while (retryCount < maxRetries && !success)
        {
            // Find the highest existing branch code number for this company
            // Use a fresh query each time to get the latest data
            var existingBranches = await _context.Branches
                .Where(b => b.CompanyId == company.Id)
                .Select(b => b.Code)
                .ToListAsync(cancellationToken);

            int maxNumber = 0;
            foreach (var code in existingBranches)
            {
                // Extract number from codes like "BR-001", "BR-002", etc.
                if (code.StartsWith("BR-") && int.TryParse(code.Substring(3), out var number))
                {
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }

            var newBranchCode = $"BR-{(maxNumber + 1):D3}"; // Format as BR-001, BR-002, etc.

            // 3. Create Branch with JSONB pattern
            // Build ContactInfo object dynamically to exclude null lat/long
            var contactInfo = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(request.ContactPhone))
                contactInfo["phone"] = request.ContactPhone;
            if (!string.IsNullOrEmpty(request.ContactEmail))
                contactInfo["email"] = request.ContactEmail;
            if (!string.IsNullOrEmpty(request.FullAddress))
                contactInfo["full_address"] = request.FullAddress;
            if (!string.IsNullOrEmpty(request.City))
                contactInfo["city"] = request.City;
            if (request.Lat.HasValue)
                contactInfo["lat"] = request.Lat.Value;
            if (request.Long.HasValue)
                contactInfo["long"] = request.Long.Value;

            branch = new Domain.Entities.Branch
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Code = newBranchCode,
                Name = request.Name,
                Slug = request.Slug,
                BranchType = request.BranchType,
                Lat = request.Lat,
                Long = request.Long,
                ContactInfo = contactInfo.Count > 0
                    ? JsonSerializer.SerializeToDocument(contactInfo)
                    : null,
                OperatingHours = request.OperatingHours,
                Settings = JsonSerializer.SerializeToDocument(new
                {
                    supports_online_order = request.SupportsOnlineOrder,
                    supports_pickup = request.SupportsPickup,
                    supports_shipping = request.SupportsShipping
                }),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Branches.Add(branch);

            try
            {
                Console.WriteLine($"[CreateBranch] Attempting to save branch with Code={branch.Code}, Slug={branch.Slug}, Attempt={retryCount + 1}");
                await _context.SaveChangesAsync(cancellationToken);
                Console.WriteLine($"[CreateBranch] SUCCESS! Branch saved: Code={branch.Code}, Id={branch.Id}");
                // Success! Break out of retry loop
                success = true;
                break;
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"[CreateBranch] DbUpdateException occurred: {ex.Message}");
                Console.WriteLine($"[CreateBranch] InnerException: {ex.InnerException?.Message}");

                // Check if it's a unique constraint violation on Code or Slug
                var isCodeConflict = ex.InnerException?.Message?.Contains("IX_branch_Code") == true ||
                                    ex.InnerException?.Message?.Contains("duplicate key") == true;
                var isSlugConflict = ex.InnerException?.Message?.Contains("IX_branch_Slug") == true;

                Console.WriteLine($"[CreateBranch] isCodeConflict={isCodeConflict}, isSlugConflict={isSlugConflict}, retryCount={retryCount}");

                if ((isCodeConflict || isSlugConflict) && retryCount < maxRetries - 1)
                {
                    Console.WriteLine($"[CreateBranch] Retrying... (attempt {retryCount + 2})");
                    // Remove the failed entity from tracking
                    _context.Branches.Remove(branch);

                    // Wait a tiny bit to avoid immediate retry
                    await Task.Delay(50 * (retryCount + 1), cancellationToken);

                    retryCount++;
                    continue; // Retry with a new code
                }

                // If it's not a code/slug conflict OR we've exhausted retries, rethrow
                Console.WriteLine($"[CreateBranch] Rethrowing exception - not a conflict or max retries reached");
                throw;
            }
        }

        if (!success || branch == null)
        {
            Console.WriteLine($"[CreateBranch] FAILED! success={success}, branch is null={branch == null}");
            throw new InvalidOperationException($"Failed to generate unique branch code after {maxRetries} attempts. Please try again.");
        }

        Console.WriteLine($"[CreateBranch] Converting to DTO and returning...");
        var dto = branch.ToDto(company.Name);
        Console.WriteLine($"[CreateBranch] DTO created successfully: Id={dto.Id}, Code={dto.Code}");
        return dto;
    }
}
