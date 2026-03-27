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

        // Update AI consultation profile fields
        userAccount.SunlightExposure = request.SunlightExposure;
        userAccount.RoomTemperatureRange = request.RoomTemperatureRange;
        userAccount.HumidityLevel = request.HumidityLevel;
        userAccount.WateringFrequency = request.WateringFrequency;
        userAccount.PlacementLocation = request.PlacementLocation;
        userAccount.SpaceSize = request.SpaceSize;
        userAccount.HasChildrenOrPets = request.HasChildrenOrPets;
        userAccount.PreferredStyle = request.PreferredStyle;
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

        // Convert PlantGoals list to JsonDocument (following the same pattern as Addresses)
        if (request.PlantGoals != null && request.PlantGoals.Count > 0)
        {
            var plantGoalsJson = JsonSerializer.Serialize(request.PlantGoals);
            userAccount.PlantGoals = JsonDocument.Parse(plantGoalsJson);
        }

        // Mark profile as completed
        userAccount.IsProfileCompleted = true;
        userAccount.UpdatedAt = DateTime.UtcNow;

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} completed onboarding profile.", request.UserId);

        return true;
    }
}
