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

        // Concatenate multiple selections into comma-separated strings only if provided
        if (request.PlacementLocation != null && request.PlacementLocation.Count > 0)
        {
            userAccount.PlacementLocation = string.Join(", ", request.PlacementLocation);
        }

        if (request.SpaceSize != null && request.SpaceSize.Count > 0)
        {
            userAccount.SpaceSize = string.Join(", ", request.SpaceSize);
        }

        if (request.HasChildrenOrPets.HasValue)
        {
            userAccount.HasChildrenOrPets = request.HasChildrenOrPets.Value;
        }

        if (request.PreferredStyle != null && request.PreferredStyle.Count > 0)
        {
            userAccount.PreferredStyle = string.Join(", ", request.PreferredStyle);
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

        // Convert PlantGoals list to JsonDocument only if provided
        if (request.PlantGoals != null && request.PlantGoals.Count > 0)
        {
            var plantGoalsJson = JsonSerializer.Serialize(request.PlantGoals);
            userAccount.PlantGoals = JsonDocument.Parse(plantGoalsJson);
        }

        userAccount.UpdatedAt = DateTime.UtcNow;
        userAccount.IsProfileCompleted = true;

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} completed onboarding profile.", request.UserId);

        return true;
    }
}
