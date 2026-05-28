using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Services;

public class ArtifactService
{
    private readonly IArtifactRepository _repository;

    public ArtifactService(IArtifactRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Artifact>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<Artifact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdWithIncludesAsync(id, cancellationToken);

    public Task<Artifact?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        _repository.GetBySlugAsync(slug, cancellationToken);

    public async Task<Artifact> CreateAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        artifact.Id = Guid.NewGuid();
        artifact.CreatedAt = DateTime.UtcNow;
        return await _repository.AddAsync(artifact, cancellationToken);
    }

    public async Task UpdateAsync(Artifact artifact, CancellationToken cancellationToken = default) =>
        await _repository.UpdateAsync(artifact, cancellationToken);

    public async Task DeleteAsync(Artifact artifact, CancellationToken cancellationToken = default) =>
        await _repository.DeleteAsync(artifact, cancellationToken);
}
