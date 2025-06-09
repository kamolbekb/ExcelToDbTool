using DataInserter.Models;

namespace DataInserter.Repositories.Interfaces;

public interface IIamRepository
{
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid> UpsertUserAsync(ExcelUser user, UserCommonFields commonFields, CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Guid>> GetExistingUsersAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);
}
