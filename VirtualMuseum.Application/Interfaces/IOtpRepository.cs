using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Interfaces;

public interface IOtpRepository
{
    Task AddAsync(EmailOtp otp, CancellationToken cancellationToken = default);
    Task<EmailOtp?> GetActiveOtpAsync(Guid userId, string code, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

