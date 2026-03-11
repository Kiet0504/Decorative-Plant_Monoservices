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

    public async Task<IotDevice?> GetDeviceBySecretAsync(string secretKey, CancellationToken cancellationToken)
    {
        return await _context.Set<IotDevice>()
            .FirstOrDefaultAsync(d => d.SecretKey == secretKey, cancellationToken);
    }

    public async Task AddSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken)
    {
        await _context.Set<SensorReading>().AddAsync(reading, cancellationToken);
    }

    public async Task<IEnumerable<IotDevice>> GetIotDevicesAsync(CancellationToken cancellationToken)
    {
        return await _context.Set<IotDevice>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IotDevice?> GetIotDeviceByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Set<IotDevice>()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
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
}
