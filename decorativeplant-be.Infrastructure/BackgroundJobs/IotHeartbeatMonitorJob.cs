using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that monitors IoT device connectivity.
/// If a device has not sent data for more than 5 minutes, it creates a "Connectivity Lost" alert.
/// </summary>
public class IotHeartbeatMonitorJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IotHeartbeatMonitorJob> _logger;
    private const int CheckIntervalMinutes = 1;
    private const int TimeoutMinutes = 5;

    public IotHeartbeatMonitorJob(IServiceProvider serviceProvider, ILogger<IotHeartbeatMonitorJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IotHeartbeatMonitorJob is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorConnectivity(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in IotHeartbeatMonitorJob.");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("IotHeartbeatMonitorJob is stopping.");
    }

    private async Task MonitorConnectivity(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var activeDevices = context.IotDevices
            .Where(d => d.Status == "Active")
            .ToList();

        foreach (var device in activeDevices)
        {
            DateTime? lastSeen = null;

            // Extract lastSeenAt from DeviceInfo JSONB
            if (device.DeviceInfo != null)
            {
                try
                {
                    var root = device.DeviceInfo.RootElement;
                    if (root.TryGetProperty("lastSeenAt", out var prop))
                    {
                        lastSeen = prop.GetDateTime().ToUniversalTime();
                    }
                }
                catch { }
            }

            // If never seen or seen too long ago
            if (lastSeen == null || (now - lastSeen.Value).TotalMinutes >= TimeoutMinutes)
            {
                await CreateConnectivityAlert(device, lastSeen, context, publisher, unitOfWork, ct);
            }
        }
    }

    private async Task CreateConnectivityAlert(IotDevice device, DateTime? lastSeen, IApplicationDbContext context, IPublisher publisher, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var actuatorKey = "connectivity_status";
        // Check for an existing unresolved alert for this device's connectivity
        var existingAlert = context.IotAlerts
            .FirstOrDefault(a => a.DeviceId == device.Id && a.ComponentKey == actuatorKey && a.ResolutionInfo == null);

        if (existingAlert != null)
        {
            // Already reported as offline. Skip creating new alert to avoid spam.
            return;
        }

        var lastSeenStr = lastSeen?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Never (Newly registered)";
        
        var newAlert = new IotAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ComponentKey = actuatorKey,
            AlertInfo = JsonSerializer.SerializeToDocument(new
            {
                severity = "CRITICAL",
                title = "Connectivity Lost",
                message = "Device is currently offline (Unplugged or No Internet).",
                description = $"The device has not responded for more than {TimeoutMinutes} minutes. Last activity: {lastSeenStr}.",
                solution = "1. Check the device power supply.\n2. Verify the WiFi connection.\n3. Ensure the device is within range of your router.",
                lastTriggeredAt = DateTime.UtcNow.ToString("o"),
                notificationCount = 1,
                lastNotificationAt = DateTime.UtcNow.ToString("o"),
                isConnectivityAlert = true
            }),
            CreatedAt = DateTime.UtcNow
        };

        context.IotAlerts.Add(newAlert);
        await unitOfWork.SaveChangesAsync(ct);

        // Notify Staff (triggers Email)
        await publisher.Publish(new decorativeplant_be.Application.Features.IoT.Events.IotAlertTriggeredNotification
        {
            Device = device,
            Alert = newAlert,
            RuleName = "HEARTBEAT MONITOR"
        }, ct);

        _logger.LogWarning("Connectivity alert created for device {DeviceId}. Last seen: {LastSeen}", device.Id, lastSeenStr);
    }
}
