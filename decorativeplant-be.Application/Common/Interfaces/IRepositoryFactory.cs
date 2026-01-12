using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IRepositoryFactory
{
    IRepository<T> CreateRepository<T>() where T : BaseEntity;
}
