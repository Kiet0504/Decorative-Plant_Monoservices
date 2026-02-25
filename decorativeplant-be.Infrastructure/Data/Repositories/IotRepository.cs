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
}
