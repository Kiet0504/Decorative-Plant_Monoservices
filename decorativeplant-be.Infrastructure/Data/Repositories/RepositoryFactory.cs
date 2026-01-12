using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Infrastructure.Data.Repositories;

public class RepositoryFactory : IRepositoryFactory
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public RepositoryFactory(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<T> CreateRepository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(Repository<>).MakeGenericType(type);
            var repository = Activator.CreateInstance(repositoryType, _context);
            if (repository == null)
            {
                throw new InvalidOperationException($"Failed to create repository for type {type.Name}");
            }
            _repositories[type] = repository;
        }

        return (IRepository<T>)_repositories[type];
    }
}
