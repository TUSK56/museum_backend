using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Interfaces;

public interface IArtifactRepository : IRepository<Artifact>
{
    Task<Artifact?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Artifact?> GetByIdWithIncludesAsync(Guid id, CancellationToken cancellationToken = default);
}
