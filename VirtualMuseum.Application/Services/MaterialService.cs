using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Services;

public class MaterialService
{
    private readonly IRepository<Material> _repository;

    public MaterialService(IRepository<Material> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Material>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<Material?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Material> CreateAsync(Material material, CancellationToken cancellationToken = default)
    {
        material.Id = Guid.NewGuid();
        return await _repository.AddAsync(material, cancellationToken);
    }

    public Task UpdateAsync(Material material, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(material, cancellationToken);

    public Task DeleteAsync(Material material, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(material, cancellationToken);
}
