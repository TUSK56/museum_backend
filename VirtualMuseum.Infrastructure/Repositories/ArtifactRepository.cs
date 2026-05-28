using Microsoft.EntityFrameworkCore;
using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.Infrastructure.Repositories;

public class ArtifactRepository : Repository<Artifact>, IArtifactRepository
{
    public ArtifactRepository(MuseumDbContext context) : base(context)
    {
    }

    public async Task<Artifact?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.Era)
            .Include(a => a.Category)
            .Include(a => a.Material)
            .Include(a => a.DiscoveryLocation)
            .Include(a => a.Translations)
            .FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);
    }

    public async Task<Artifact?> GetByIdWithIncludesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.Era)
            .Include(a => a.Category)
            .Include(a => a.Material)
            .Include(a => a.DiscoveryLocation)
            .Include(a => a.ModelFile)
            .Include(a => a.ThumbnailFile)
            .Include(a => a.Translations)
            .Include(a => a.ArtifactTags)
                .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public override async Task<IEnumerable<Artifact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.Era)
            .Include(a => a.Category)
            .Include(a => a.Material)
            .Include(a => a.DiscoveryLocation)
            .Include(a => a.ModelFile)
            .Include(a => a.ThumbnailFile)
            .Include(a => a.Translations)
            .ToListAsync(cancellationToken);
    }
}
