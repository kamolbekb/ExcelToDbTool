using DataInserter.Models;

namespace DataInserter.Services.Interfaces;

public interface IExcelReaderService
{
    Task<List<ExcelUser>> ReadUsersFromExcelAsync(string filePath, CancellationToken cancellationToken = default);
    IAsyncEnumerable<List<ExcelUser>> ReadUsersInBatchesAsync(string filePath, int batchSize, CancellationToken cancellationToken = default);
}
