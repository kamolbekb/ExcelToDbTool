using ClosedXML.Excel;
using DataInserter.Models;
using DataInserter.Services.Interfaces;
using DataInserter.Utilities;
using Serilog;

namespace DataInserter.Services.Implementations;

public class ExcelReaderService : IExcelReaderService
{
    private readonly ILogger _logger;

    public ExcelReaderService(ILogger logger)
    {
        _logger = logger.ForContext<ExcelReaderService>();
    }

    public async Task<List<ExcelUser>> ReadUsersFromExcelAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ReadUsersFromExcel(filePath), cancellationToken);
    }

    public async IAsyncEnumerable<List<ExcelUser>> ReadUsersInBatchesAsync(
        string filePath, 
        int batchSize, 
        CancellationToken cancellationToken = default)
    {
        var users = new List<ExcelUser>();
        
        await foreach (var user in ReadUsersStreamAsync(filePath, cancellationToken))
        {
            users.Add(user);
            
            if (users.Count >= batchSize)
            {
                yield return users;
                users = new List<ExcelUser>();
            }
        }

        if (users.Any())
        {
            yield return users;
        }
    }

    private async IAsyncEnumerable<ExcelUser> ReadUsersStreamAsync(string filePath, CancellationToken cancellationToken)
    {
        await Task.Yield(); // Make it truly async

        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RangeUsed()?.RowsUsed();

        if (rows == null)
        {
            _logger.Warning("No data found in Excel file");
            yield break;
        }

        var rowNumber = 0;
        foreach (var row in rows.Skip(2)) // Skip header rows
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            ExcelUser? user = null;
            try
            {
                user = ParseUserFromRow(row, rowNumber);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error parsing row {RowNumber}", rowNumber);
            }

            if (user != null && user.IsValid())
            {
                yield return user;
            }
        }
    }

    private List<ExcelUser> ReadUsersFromExcel(string filePath)
    {
        var users = new List<ExcelUser>();

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed()?.RowsUsed();

            if (rows == null)
            {
                _logger.Warning("No data found in Excel file");
                return users;
            }

            var rowNumber = 0;
            foreach (var row in rows.Skip(2)) // Skip header rows
            {
                rowNumber++;
                try
                {
                    var user = ParseUserFromRow(row, rowNumber);
                    if (user != null && user.IsValid())
                    {
                        users.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error reading row {RowNumber}", rowNumber);
                }
            }

            _logger.Information("Read {Count} valid users from Excel", users.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading Excel file: {FilePath}", filePath);
            throw;
        }

        return users;
    }

    private ExcelUser? ParseUserFromRow(IXLRangeRow row, int dataRowNumber)
    {
        try
        {
            var user = new ExcelUser
            {
                ExcelRow = row.Cell(1).TryGetValue<int>(out var rowNum) ? rowNum : dataRowNumber,
                Name = row.Cell(2).GetString().Trim(),
                Email = row.Cell(3).GetString().Trim(),
                Role = row.Cell(5).GetString().Trim(),
                UserGroup = row.Cell(6).GetString().Trim(),
                Section = row.Cell(7).GetString().Trim(),
                Division = row.Cell(8).GetString().Trim(),
                ControlLevel = RoleMapper.ParseControlLevel("SECTION") // Default to SECTION
            };

            return user;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to parse user from row {RowNumber}", dataRowNumber);
            return null;
        }
    }
}
