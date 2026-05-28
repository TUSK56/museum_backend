using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Services;

public class EraService
{
    private readonly IRepository<Era> _repository;

    public EraService(IRepository<Era> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Era>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<Era?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Era> CreateAsync(Era era, CancellationToken cancellationToken = default)
    {
        era.Id = Guid.NewGuid();
        return await _repository.AddAsync(era, cancellationToken);
    }

    public Task UpdateAsync(Era era, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(era, cancellationToken);

    public Task DeleteAsync(Era era, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(era, cancellationToken);
}
