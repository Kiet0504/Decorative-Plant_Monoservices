using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Auth.Commands;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class CompleteProfileCommandHandler : IRequestHandler<CompleteProfileCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CompleteProfileCommandHandler> _logger;

    public CompleteProfileCommandHandler(
        IApplicationDbContext context,
        ILogger<CompleteProfileCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> Handle(CompleteProfileCommand request, CancellationToken cancellationToken)
    {
        // Get user account
        var userAccount = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (userAccount == null)
        {
            throw new NotFoundException("User account not found.");
        }

        // Update AI consultation profile fields only if provided
        if (!string.IsNullOrWhiteSpace(request.SunlightExposure))
            userAccount.SunlightExposure = request.SunlightExposure;
            
        if (!string.IsNullOrWhiteSpace(request.RoomTemperatureRange))
            userAccount.RoomTemperatureRange = request.RoomTemperatureRange;
            
        if (!string.IsNullOrWhiteSpace(request.HumidityLevel))
            userAccount.HumidityLevel = request.HumidityLevel;
            
        if (!string.IsNullOrWhiteSpace(request.WateringFrequency))
            userAccount.WateringFrequency = request.WateringFrequency;

        // Multi-select: null = leave unchanged; empty list = clear; non-empty = replace
        if (request.PlacementLocation != null)
        {
            userAccount.PlacementLocation = request.PlacementLocation.Count > 0
                ? string.Join(", ", request.PlacementLocation)
                : null;
        }

        if (request.SpaceSize != null)
        {
            userAccount.SpaceSize = request.SpaceSize.Count > 0
                ? string.Join(", ", request.SpaceSize)
                : null;
        }

        if (request.HasChildrenOrPets.HasValue)
        {
            userAccount.HasChildrenOrPets = request.HasChildrenOrPets.Value;
        }

        if (request.PreferredStyle != null)
        {
            userAccount.PreferredStyle = request.PreferredStyle.Count > 0
                ? string.Join(", ", request.PreferredStyle)
                : null;
        }

        if (!string.IsNullOrWhiteSpace(request.BudgetRange))
            userAccount.BudgetRange = request.BudgetRange;

        // Update ExperienceLevel if provided
        if (!string.IsNullOrWhiteSpace(request.ExperienceLevel))
        {
            userAccount.ExperienceLevel = request.ExperienceLevel;
        }

        // Update LocationCity if provided
        if (!string.IsNullOrWhiteSpace(request.LocationCity))
        {
            userAccount.LocationCity = request.LocationCity;
        }

        // Update FullName (DisplayName) if provided
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            userAccount.DisplayName = request.FullName;
        }

        // Update Phone if provided
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            userAccount.Phone = request.Phone;
        }

        if (request.Bio != null)
            userAccount.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();

        if (request.AvatarUrl != null)
            userAccount.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();

        if (request.HardinessZone != null)
            userAccount.HardinessZone = string.IsNullOrWhiteSpace(request.HardinessZone) ? null : request.HardinessZone.Trim();

        if (request.PlantGoals != null)
        {
            if (request.PlantGoals.Count > 0)
            {
                var plantGoalsJson = JsonSerializer.Serialize(request.PlantGoals);
                userAccount.PlantGoals = JsonDocument.Parse(plantGoalsJson);
            }
            else
            {
                userAccount.PlantGoals = null;
            }
        }

        userAccount.UpdatedAt = DateTime.UtcNow;
        userAccount.IsProfileCompleted = true;

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} completed onboarding profile.", request.UserId);

        return true;
    }
}
