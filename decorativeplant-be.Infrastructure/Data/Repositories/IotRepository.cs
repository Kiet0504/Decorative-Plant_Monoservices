using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Infrastructure.Data.Repositories;

public class IotRepository : IIotRepository
{
    private readonly ApplicationDbContext _context;

    public IotRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // --- SensorReading ---
    public async Task<IotDevice?> GetDeviceBySecretAsync(string secretKey, CancellationToken cancellationToken)
    {
        return await _context.Set<IotDevice>()
            .FirstOrDefaultAsync(d => d.SecretKey == secretKey, cancellationToken);
    }

    public async Task AddSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken)
    {
        await _context.Set<SensorReading>().AddAsync(reading, cancellationToken);
    }

    public async Task<IEnumerable<SensorReading>> GetSensorMetricsAsync(Guid deviceId, string? componentKey, DateTime? startTime, DateTime? endTime, CancellationToken cancellationToken)
    {
        var query = _context.Set<SensorReading>().AsNoTracking().Where(r => r.DeviceId == deviceId);
        if (!string.IsNullOrEmpty(componentKey))
            query = query.Where(r => r.ComponentKey == componentKey);
        if (startTime.HasValue)
            query = query.Where(r => r.RecordedAt >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(r => r.RecordedAt <= endTime.Value);
        return await query.OrderByDescending(r => r.RecordedAt).ToListAsync(cancellationToken);
    }

    // --- IotDevice ---
    public async Task<IEnumerable<IotDevice>> GetIotDevicesAsync(CancellationToken cancellationToken)
    {
        return await _context.Set<IotDevice>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IotDevice?> GetIotDeviceByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Set<IotDevice>().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IotDevice> CreateIotDeviceAsync(IotDevice device, CancellationToken cancellationToken)
    {
        await _context.Set<IotDevice>().AddAsync(device, cancellationToken);
        return device;
    }

    public Task UpdateIotDeviceAsync(IotDevice device, CancellationToken cancellationToken)
    {
        _context.Set<IotDevice>().Update(device);
        return Task.CompletedTask;
    }

    public Task DeleteIotDeviceAsync(IotDevice device, CancellationToken cancellationToken)
    {
        _context.Set<IotDevice>().Remove(device);
        return Task.CompletedTask;
    }

    // --- IotAlert ---
    public async Task<IEnumerable<IotAlert>> GetIotAlertsAsync(Guid? deviceId, CancellationToken cancellationToken)
    {
        var query = _context.Set<IotAlert>().AsNoTracking();
        if (deviceId.HasValue)
            query = query.Where(a => a.DeviceId == deviceId.Value);
        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IotAlert?> GetIotAlertByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Set<IotAlert>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IotAlert> CreateIotAlertAsync(IotAlert alert, CancellationToken cancellationToken)
    {
        await _context.Set<IotAlert>().AddAsync(alert, cancellationToken);
        return alert;
    }

    public Task UpdateIotAlertAsync(IotAlert alert, CancellationToken cancellationToken)
    {
        _context.Set<IotAlert>().Update(alert);
        return Task.CompletedTask;
    }

    public Task DeleteIotAlertAsync(IotAlert alert, CancellationToken cancellationToken)
    {
        _context.Set<IotAlert>().Remove(alert);
        return Task.CompletedTask;
    }

    // --- AutomationRule ---
    public async Task<IEnumerable<AutomationRule>> GetAutomationRulesAsync(Guid? deviceId, CancellationToken cancellationToken)
    {
        var query = _context.Set<AutomationRule>().AsNoTracking();
        if (deviceId.HasValue)
            query = query.Where(r => r.DeviceId == deviceId.Value);
        return await query.OrderByDescending(r => r.Priority).ToListAsync(cancellationToken);
    }

    public async Task<AutomationRule?> GetAutomationRuleByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Set<AutomationRule>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<AutomationRule> CreateAutomationRuleAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        await _context.Set<AutomationRule>().AddAsync(rule, cancellationToken);
        return rule;
    }

    public Task UpdateAutomationRuleAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        _context.Set<AutomationRule>().Update(rule);
        return Task.CompletedTask;
    }

    public Task DeleteAutomationRuleAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        _context.Set<AutomationRule>().Remove(rule);
        return Task.CompletedTask;
    }

    // --- AutomationExecutionLog (Read-only) ---
    public async Task<IEnumerable<AutomationExecutionLog>> GetExecutionLogsAsync(Guid? ruleId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var query = _context.Set<AutomationExecutionLog>().AsNoTracking();
        if (ruleId.HasValue)
            query = query.Where(l => l.RuleId == ruleId.Value);
        if (from.HasValue)
            query = query.Where(l => l.ExecutedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.ExecutedAt <= to.Value);
        return await query.OrderByDescending(l => l.ExecutedAt).ToListAsync(cancellationToken);
    }

    public async Task<AutomationExecutionLog?> GetExecutionLogByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Set<AutomationExecutionLog>().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task AddExecutionLogAsync(AutomationExecutionLog log, CancellationToken cancellationToken)
    {
        await _context.Set<AutomationExecutionLog>().AddAsync(log, cancellationToken);
    }
}
