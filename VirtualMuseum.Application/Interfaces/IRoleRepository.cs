using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Interfaces;

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
