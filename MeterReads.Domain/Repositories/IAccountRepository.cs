using MeterReading.Domain.Entities;

namespace MeterReading.Domain.Repositories
{
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(int accountId, CancellationToken cancellationToken = default);
        Task<List<Account>> GetByIdsAsync(IEnumerable<int> accountIds, CancellationToken cancellationToken = default);
        Task AddAsync(Account account, CancellationToken cancellationToken = default);
        Task UpdateAsync(Account account, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(int accountId, CancellationToken cancellationToken = default);
        Task<List<int>> GetAllAccountIdsAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}