using DataInserter.Models;

namespace DataInserter.Services.Interfaces;

public interface IUserProcessingService
{
    Task<ProcessingResult> ProcessUsersAsync(List<ExcelUser> users, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ProcessUsersBatchAsync(List<ExcelUser> users, CancellationToken cancellationToken = default);
}
