using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Infrastructure.Data.Repositories;

public class RepositoryFactory : IRepositoryFactory
{
    private readonly ApplicationDbContext _context;

    public RepositoryFactory(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<T> CreateRepository<T>() where T : class
    {
        return new Repository<T>(_context);
    }
}
