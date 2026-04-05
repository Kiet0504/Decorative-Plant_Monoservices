using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;

public class IngestSensorDataCommandHandler : IRequestHandler<IngestSensorDataCommand, bool>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IConfiguration _configuration;

    public IngestSensorDataCommandHandler(
        IIotRepository iotRepository, 
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IEmailTemplateService emailTemplateService,
        IConfiguration configuration)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _emailTemplateService = emailTemplateService;
        _configuration = configuration;
    }

    public async Task<bool> Handle(IngestSensorDataCommand request, CancellationToken cancellationToken)
    {
        var device = await _iotRepository.GetDeviceBySecretAsync(request.DeviceSecret, cancellationToken);
        if (device == null || device.Status != "Active")
        {
            throw new UnauthorizedAccessException("Invalid or inactive device.");
        }

        var reading = new SensorReading
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ComponentKey = request.ComponentKey,
            Value = request.Value,
            RecordedAt = DateTime.UtcNow
        };

        await _iotRepository.AddSensorReadingAsync(reading, cancellationToken);

        // --- Automatic Alert Generation ---
        var rules = await _iotRepository.GetAutomationRulesAsync(device.Id, cancellationToken);
        var activeRules = rules.Where(r => r.IsActive);

        foreach (var rule in activeRules)
        {
            if (rule.Conditions == null) continue;

            try
            {
                var conditions = JsonSerializer.Deserialize<List<JsonElement>>(rule.Conditions.RootElement.GetRawText());
                if (conditions == null) continue;

                foreach (var condition in conditions)
                {
                    var compKey = condition.GetProperty("component_key").GetString();
                    if (compKey != request.ComponentKey) continue;

                    var op = condition.GetProperty("operator").GetString();
                    var thresholdStr = condition.GetProperty("threshold").GetRawText();
                    if (!decimal.TryParse(thresholdStr, out decimal threshold)) continue;

                    bool triggered = op switch
                    {
                        ">" => request.Value > threshold,
                        "<" => request.Value < threshold,
                        "=" => request.Value == threshold,
                        ">=" => request.Value >= threshold,
                        "<=" => request.Value <= threshold,
                        _ => false
                    };

                    if (triggered)
                    {
                        // --- De-duplication Logic ---
                        // Check if there's already an unresolved alert for this device + component
                        var existingUnresolvedAlert = await _context.Set<IotAlert>()
                            .FirstOrDefaultAsync(a => a.DeviceId == device.Id 
                                && a.ComponentKey == request.ComponentKey 
                                && a.ResolutionInfo == null, cancellationToken);

                        if (existingUnresolvedAlert != null)
                        {
                            // An unresolved alert already exists for this sensor. 
                            // Skip creating a new record and skip sending a new email to prevent bloat.
                            continue;
                        }

                        var solution = condition.TryGetProperty("solution", out var sol) ? sol.GetString() : "Check sensor and environment manually.";
                        
                        var alert = new IotAlert
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            ComponentKey = request.ComponentKey,
                            AlertInfo = JsonSerializer.SerializeToDocument(new
                            {
                                type = "threshold_exceeded",
                                severity = "critical",
                                title = $"Threshold Breached: {rule.Name}",
                                message = $"Rule '{rule.Name}' detected a violation. Value {request.Value} is {op} than threshold {threshold}.",
                                sensor = request.ComponentKey,
                                solution = solution,
                                values = new { current = request.Value, threshold = threshold, op = op }
                            }),
                            CreatedAt = DateTime.UtcNow
                        };
                        await _iotRepository.CreateIotAlertAsync(alert, cancellationToken);

                        // --- Notification Logic ---
                        if (device.BranchId.HasValue)
                        {
                            var staffEmails = await _context.StaffAssignments
                                .Where(s => s.BranchId == device.BranchId)
                                .Select(s => new { s.Staff.Email, s.Staff.DisplayName })
                                .Where(s => !string.IsNullOrEmpty(s.Email))
                                .ToListAsync(cancellationToken);

                            // Generate secure token for one-click action
                            var action = "water_now";
                            var tokenInput = device.Id.ToString() + action + device.SecretKey;
                            var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(tokenInput));
                            var actionToken = Convert.ToHexString(tokenHash).ToLower();
                            
                            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
                            var waterNowUrl = $"{apiBaseUrl}/api/public/iot/execute-action?deviceId={device.Id}&action={action}&token={actionToken}";

                            foreach (var staff in staffEmails)
                            {
                                try
                                {
                                    var emailModel = new Dictionary<string, string>
                                    {
                                        { "RuleName", rule.Name ?? "Automation Rule" },
                                        { "DeviceName", device.DeviceInfo?.RootElement.TryGetProperty("name", out var dn) == true ? dn.GetString() ?? "Unknown Device" : "Unknown Device" },
                                        { "Value", $"{request.Value}" },
                                        { "Threshold", $"{op} {threshold}" },
                                        { "Solution", solution ?? "Manual intervention required." },
                                        { "OccurredAt", DateTime.UtcNow.ToString("f") },
                                        { "DashboardUrl", "https://decorativeplant.com/cultivation/alerts" },
                                        { "WaterNowUrl", waterNowUrl }
                                    };

                                    await _emailTemplateService.SendTemplateAsync(
                                        "IotAlert",
                                        emailModel,
                                        staff.Email!,
                                        $"[CRITICAL ALERT] {rule.Name} - {request.ComponentKey} Violation",
                                        staff.DisplayName,
                                        cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to send alert email to {staff.Email}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail ingestion
                Console.WriteLine($"Error evaluating automation rule {rule.Id}: {ex.Message}");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
