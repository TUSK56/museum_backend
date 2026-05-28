using Microsoft.EntityFrameworkCore;
using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.Infrastructure.Repositories;

public class PendingUserRegistrationRepository : IPendingUserRegistrationRepository
{
    private readonly MuseumDbContext _context;

    public PendingUserRegistrationRepository(MuseumDbContext context)
    {
        _context = context;
    }

    public Task<PendingUserRegistration?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.PendingUserRegistrations
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public Task<PendingUserRegistration?> GetActiveByEmailAndOtpAsync(string email, string otpCode, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return _context.PendingUserRegistrations
            .Where(x => x.Email == email && x.OtpCode == otpCode && !x.IsVerified && x.OtpExpirationTime >= now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(PendingUserRegistration pendingRegistration, CancellationToken cancellationToken = default)
    {
        await _context.PendingUserRegistrations.AddAsync(pendingRegistration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PendingUserRegistration pendingRegistration, CancellationToken cancellationToken = default)
    {
        _context.PendingUserRegistrations.Update(pendingRegistration);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PendingUserRegistration pendingRegistration, CancellationToken cancellationToken = default)
    {
        _context.PendingUserRegistrations.Remove(pendingRegistration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
