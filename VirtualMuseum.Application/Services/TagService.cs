using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Services;

public class TagService
{
    private readonly IRepository<Tag> _repository;

    public TagService(IRepository<Tag> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Tag>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Tag> CreateAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        tag.Id = Guid.NewGuid();
        return await _repository.AddAsync(tag, cancellationToken);
    }

    public Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(tag, cancellationToken);

    public Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(tag, cancellationToken);
}
