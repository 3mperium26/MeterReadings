using MeterReading.Domain.Entities;
using MeterReading.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MeterReading.Infrastructure.Persistence.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountRepository> _logger;

        public AccountRepository(ApplicationDbContext context, ILogger<AccountRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Account?> GetByIdAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                                 .Include(a => a.MeterReadings)
                                 .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
        }
        public async Task<List<Account>> GetByIdsAsync(IEnumerable<int> accountIds, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                                 .Where(a => accountIds.Contains(a.AccountId))
                                 .Include(a => a.MeterReadings)
                                 .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            await _context.Accounts.AddAsync(account, cancellationToken);
        }

        public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
        {
            _context.Accounts.Update(account);
            return Task.CompletedTask;
        }
        public async Task<bool> ExistsAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts.AnyAsync(a => a.AccountId == accountId, cancellationToken);
        }

        public async Task<List<int>> GetAllAccountIdsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Accounts.Select(a => a.AccountId).ToListAsync(cancellationToken);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "A concurrency error occurred while saving changes.");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "A database update error occurred while saving changes. Inner: {InnerMessage}", ex.InnerException?.Message);
                throw;
            }
        }
    }
}
