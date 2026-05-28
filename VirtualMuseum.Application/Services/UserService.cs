using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Services;

public class UserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<User> CreateAsync(User user, string plainPassword, CancellationToken cancellationToken = default)
    {
        user.Id = Guid.NewGuid();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        user.CreatedAt = DateTime.UtcNow;
        return await _repository.AddAsync(user, cancellationToken);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(user, cancellationToken);

    public Task DeleteAsync(User user, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(user, cancellationToken);
}
