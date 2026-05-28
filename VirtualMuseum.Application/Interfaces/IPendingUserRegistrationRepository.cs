using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Interfaces;

public interface IPendingUserRegistrationRepository
{
    Task<PendingUserRegistration?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PendingUserRegistration?> GetActiveByEmailAndOtpAsync(string email, string otpCode, CancellationToken cancellationToken = default);
    Task AddAsync(PendingUserRegistration pendingRegistration, CancellationToken cancellationToken = default);
    Task UpdateAsync(PendingUserRegistration pendingRegistration, CancellationToken cancellationToken = default);
    Task DeleteAsync(PendingUserRegistration pendingRegistration, CancellationToken cancellationToken = default);
}
