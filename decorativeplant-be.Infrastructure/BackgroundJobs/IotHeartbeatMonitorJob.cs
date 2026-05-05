using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that monitors IoT device connectivity AND individual sensor activity.
/// - If a device has not sent any data for more than 5 minutes → "Connectivity Lost" alert.
/// - If a specific sensor component has not sent data for more than 5 minutes → "Sensor Silent" alert.
/// </summary>
public class IotHeartbeatMonitorJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IotHeartbeatMonitorJob> _logger;
    private const int CheckIntervalMinutes = 1;
    private const int TimeoutMinutes = 3; // Tăng lên 3 phút để tránh báo động giả do lag mạng

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
        var iotRepository = scope.ServiceProvider.GetRequiredService<IIotRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var activeDevices = context.IotDevices
            .Include(d => d.Location)
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
                        lastSeen = prop.GetDateTime().ToUniversalTime();
                }
                catch { }
            }

            // 1. Check overall device connectivity
            if (lastSeen == null || (now - lastSeen.Value).TotalMinutes >= TimeoutMinutes)
            {
                await CreateConnectivityAlert(device, lastSeen, context, publisher, unitOfWork, ct);
                // Khôi phục continue: Nếu cả bộ ESP offline thì chỉ báo 1 lỗi Connectivity thôi cho đỡ spam
                continue;
            }

            // 2. Device is online → check each sensor component individually
            await CheckSensorComponents(device, now, context, iotRepository, publisher, unitOfWork, ct);
        }
    }

    /// <summary>
    /// For each sensor-type component registered on the device, check if it has sent data recently.
    /// If a component has been silent for >= TimeoutMinutes, create a "Sensor Silent" alert.
    /// </summary>
    private async Task CheckSensorComponents(
        IotDevice device, DateTime now,
        IApplicationDbContext context,
        IIotRepository iotRepository,
        IPublisher publisher, IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        // Extract component list from DeviceInfo or Components JSONB
        List<(string key, string name)> sensorComponents = new();

        try
        {
            JsonElement compRoot = default;
            bool hasComponents = false;

            if (device.DeviceInfo != null &&
                device.DeviceInfo.RootElement.TryGetProperty("components", out compRoot))
                hasComponents = true;

            // Try top-level Components field if DeviceInfo doesn't have it
            if (!hasComponents && device.Components != null)
            {
                compRoot = device.Components.RootElement;
                hasComponents = true;
            }

            if (hasComponents)
            {
                if (compRoot.ValueKind == JsonValueKind.Array)
                {
                    foreach (var comp in compRoot.EnumerateArray())
                    {
                        var compType = comp.TryGetProperty("type", out var t) ? t.GetString() : null;
                        if (!string.Equals(compType, "sensor", StringComparison.OrdinalIgnoreCase)) continue;

                        var key = comp.TryGetProperty("key", out var k) ? k.GetString() : null;
                        var name = comp.TryGetProperty("name", out var n) ? n.GetString() : key;
                        if (!string.IsNullOrEmpty(key))
                            sensorComponents.Add((key!, name ?? key!));
                    }
                }
                else if (compRoot.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in compRoot.EnumerateObject())
                    {
                        var compType = prop.Value.TryGetProperty("type", out var t) ? t.GetString() : null;
                        if (!string.Equals(compType, "sensor", StringComparison.OrdinalIgnoreCase)) continue;
                        var name = prop.Value.TryGetProperty("name", out var n) ? n.GetString() : prop.Name;
                        sensorComponents.Add((prop.Name, name ?? prop.Name));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse components for device {DeviceId}", device.Id);
            return;
        }

        foreach (var (compKey, compName) in sensorComponents)
        {
            try
            {
                // Get latest reading for this specific component within the timeout window
                var cutoff = now.AddMinutes(-TimeoutMinutes);
                var recentReadings = await iotRepository.GetSensorMetricsAsync(
                    device.Id, compKey, cutoff, now, ct);

                // Filter out ERROR readings — only count real data
                var validReadings = recentReadings
                    .Where(r => !string.Equals(r.Value.ToString(), "ERROR", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!validReadings.Any())
                {
                    // This sensor has been silent for >= TimeoutMinutes while the device is online
                    await CreateSensorSilentAlert(device, compKey, compName, context, publisher, unitOfWork, ct);
                }
                else
                {
                    // Sensor is reporting → auto-resolve any existing "Sensor Silent" alert for it
                    await AutoResolveSensorAlert(device.Id, compKey, context, unitOfWork, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking sensor component '{CompKey}' for device {DeviceId}", compKey, device.Id);
            }
        }
    }

    private async Task CreateSensorSilentAlert(
        IotDevice device, string compKey, string compName,
        IApplicationDbContext context, IPublisher publisher,
        IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var alertComponentKey = $"sensor_silent_{compKey}";

        var existingAlert = context.IotAlerts
            .FirstOrDefault(a => a.DeviceId == device.Id && a.ComponentKey == alertComponentKey && a.ResolutionInfo == null);

        if (existingAlert != null) return; // Already alerted, skip spam

        var newAlert = new IotAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ComponentKey = alertComponentKey,
            AlertInfo = JsonSerializer.SerializeToDocument(new
            {
                severity = "WARNING",
                title = "Sensor Not Responding",
                message = $"Sensor '{compName}' has not sent data for more than {TimeoutMinutes} minutes.",
                description = $"The device is online but component '{compKey}' is silent. The sensor may be disconnected or damaged.",
                solution = $"1. Check the wiring for '{compName}'.\n2. Verify the sensor is powered.\n3. Replace the sensor if the issue persists.",
                lastTriggeredAt = DateTime.UtcNow.ToString("o"),
                notificationCount = 1,
                lastNotificationAt = DateTime.UtcNow.ToString("o"),
                isSensorSilentAlert = true,
                componentKey = compKey
            }),
            CreatedAt = DateTime.UtcNow
        };

        context.IotAlerts.Add(newAlert);
        await unitOfWork.SaveChangesAsync(ct);

        await publisher.Publish(new decorativeplant_be.Application.Features.IoT.Events.IotAlertTriggeredNotification
        {
            Device = device,
            Alert = newAlert,
            RuleName = "SENSOR MONITOR"
        }, ct);

        _logger.LogWarning("Sensor silent alert created for component '{CompKey}' on device {DeviceId}", compKey, device.Id);
    }

    private async Task AutoResolveSensorAlert(
        Guid deviceId, string compKey,
        IApplicationDbContext context, IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var alertComponentKey = $"sensor_silent_{compKey}";
        var existingAlert = context.IotAlerts
            .FirstOrDefault(a => a.DeviceId == deviceId && a.ComponentKey == alertComponentKey && a.ResolutionInfo == null);

        if (existingAlert == null) return;

        existingAlert.ResolutionInfo = JsonSerializer.SerializeToDocument(new
        {
            resolvedAt = DateTime.UtcNow.ToString("o"),
            resolvedBy = "SYSTEM",
            note = "Sensor resumed sending data automatically."
        });

        await unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Sensor silent alert auto-resolved for component '{CompKey}' on device {DeviceId}", compKey, deviceId);
    }

    private async Task CreateConnectivityAlert(IotDevice device, DateTime? lastSeen, IApplicationDbContext context, IPublisher publisher, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var actuatorKey = "connectivity_status";
        var existingAlert = context.IotAlerts
            .FirstOrDefault(a => a.DeviceId == device.Id && a.ComponentKey == actuatorKey && a.ResolutionInfo == null);

        if (existingAlert != null) return;

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

        await publisher.Publish(new decorativeplant_be.Application.Features.IoT.Events.IotAlertTriggeredNotification
        {
            Device = device,
            Alert = newAlert,
            RuleName = "HEARTBEAT MONITOR"
        }, ct);

        _logger.LogWarning("Connectivity alert created for device {DeviceId}. Last seen: {LastSeen}", device.Id, lastSeenStr);
    }
}
