using DataInserter.Models;

namespace DataInserter.Services.Interfaces;

public interface IDuplicateHandlerService
{
    Task InitializeDuplicateFileAsync(CancellationToken cancellationToken = default);
    Task LogDuplicateAsync(DuplicateRecord duplicate, CancellationToken cancellationToken = default);
    Task<string> GetDuplicateFilePath();
}
