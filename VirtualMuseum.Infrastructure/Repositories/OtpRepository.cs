using Microsoft.EntityFrameworkCore;
using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.Infrastructure.Repositories;

public class OtpRepository : IOtpRepository
{
    private readonly MuseumDbContext _context;

    public OtpRepository(MuseumDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailOtp otp, CancellationToken cancellationToken = default)
    {
        await _context.EmailOtps.AddAsync(otp, cancellationToken);
    }

    public async Task<EmailOtp?> GetActiveOtpAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.EmailOtps
            .Where(o => o.UserId == userId && o.Code == code && !o.IsUsed && o.ExpirationTime >= now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}

