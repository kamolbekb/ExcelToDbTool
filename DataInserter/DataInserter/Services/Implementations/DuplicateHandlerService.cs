using DataInserter.Constants;
using DataInserter.Models;
using DataInserter.Services.Interfaces;
using Serilog;
using System.Text;

namespace DataInserter.Services.Implementations;

public class DuplicateHandlerService : IDuplicateHandlerService
{
    private readonly ILogger _logger;
    private readonly string _projectRoot;
    private string? _duplicateFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public DuplicateHandlerService(ILogger logger)
    {
        _logger = logger.ForContext<DuplicateHandlerService>();
        var basePath = AppContext.BaseDirectory;
        // Use Path.Combine for cross-platform compatibility
        _projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
    }

    public async Task InitializeDuplicateFileAsync(CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH-mm-ss");
        var duplicateRecordsDir = Path.Combine(_projectRoot, ApplicationConstants.DirectoryNames.DuplicateRecords);
        
        Directory.CreateDirectory(duplicateRecordsDir);
        
        _duplicateFilePath = Path.Combine(
            duplicateRecordsDir, 
            string.Format(ApplicationConstants.LogFilePatterns.DuplicatesLog, timestamp));

        await File.WriteAllTextAsync(_duplicateFilePath, string.Empty, cancellationToken);
        
        _logger.Information("Duplicate file created at: {FilePath}", _duplicateFilePath);

        // Update .gitignore
        await UpdateGitignoreAsync(cancellationToken);
    }

    public async Task LogDuplicateAsync(DuplicateRecord duplicate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_duplicateFilePath))
        {
            throw new InvalidOperationException("Duplicate file not initialized. Call InitializeDuplicateFileAsync first.");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var logLine = new StringBuilder()
                .AppendLine($"[Row {duplicate.ExcelRow}]")
                .AppendLine($"  Email: {duplicate.Email}")
                .AppendLine($"  Existing User ID: {duplicate.ExistingUserId}")
                .AppendLine($"  Detected At: {duplicate.DetectedAt:yyyy-MM-dd HH:mm:ss}")
                .AppendLine(new string('-', 50))
                .ToString();

            await File.AppendAllTextAsync(_duplicateFilePath, logLine, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<string> GetDuplicateFilePath()
    {
        if (string.IsNullOrEmpty(_duplicateFilePath))
        {
            throw new InvalidOperationException("Duplicate file not initialized.");
        }

        return Task.FromResult(_duplicateFilePath);
    }

    private async Task UpdateGitignoreAsync(CancellationToken cancellationToken)
    {
        var gitignorePath = Path.Combine(AppContext.BaseDirectory, ".gitignore");
        var relativeIgnorePath = Path.Combine(
            ApplicationConstants.DirectoryNames.DuplicateRecords, 
            "duplicates_*.txt");

        try
        {
            if (!File.Exists(gitignorePath))
            {
                await File.WriteAllTextAsync(gitignorePath, relativeIgnorePath + Environment.NewLine, cancellationToken);
                return;
            }

            var lines = await File.ReadAllLinesAsync(gitignorePath, cancellationToken);
            if (!lines.Any(line => line.Trim() == relativeIgnorePath))
            {
                await File.AppendAllTextAsync(gitignorePath, Environment.NewLine + relativeIgnorePath, cancellationToken);
                _logger.Information(".gitignore updated to ignore: {Pattern}", relativeIgnorePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to update .gitignore");
        }
    }
}
