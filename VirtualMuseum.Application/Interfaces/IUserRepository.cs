using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
