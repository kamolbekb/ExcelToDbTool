using DataInserter.Configuration;
using DataInserter.Constants;
using DataInserter.Extensions;
using DataInserter.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System.Diagnostics;

namespace DataInserter;

class Program
{
    static async Task Main(string[] args)
    {
        // Configure Serilog early
        var timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH-mm-ss");
        var logPath = Path.Combine(
            GetProjectRoot(),
            ApplicationConstants.DirectoryNames.Logs,
            string.Format(ApplicationConstants.LogFilePatterns.DataInserterLog, timestamp));

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Infinite)
            .CreateLogger();

        try
        {
            Log.Information("Application starting...");
            Log.Information("Log file created at: {LogPath}", logPath);

            var host = CreateHostBuilder(args).Build();

            await RunApplicationAsync(host);

            Log.Information("Application completed successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            Environment.Exit(1);
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory)
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Register Serilog Logger first
                services.AddSingleton<ILogger>(Log.Logger);
                services.AddDataInserterServices(context.Configuration);
            });

    static async Task RunApplicationAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        var configuration = services.GetRequiredService<IConfiguration>();
        var appConfig = services.GetRequiredService<IOptions<ApplicationConfiguration>>().Value;
        var logger = services.GetRequiredService<ILogger>().ForContext<Program>();

        var excelReader = services.GetRequiredService<IExcelReaderService>();
        var userProcessor = services.GetRequiredService<IUserProcessingService>();
        var duplicateHandler = services.GetRequiredService<IDuplicateHandlerService>();

        // Validate configuration
        if (string.IsNullOrWhiteSpace(appConfig.ExcelPath) || !File.Exists(appConfig.ExcelPath))
        {
            logger.Error("Invalid Excel file path: {Path}", appConfig.ExcelPath);
            throw new InvalidOperationException($"Excel file not found: {appConfig.ExcelPath}");
        }

        // Initialize duplicate handler
        await duplicateHandler.InitializeDuplicateFileAsync();
        logger.Information("Duplicate file created at: {Path}", await duplicateHandler.GetDuplicateFilePath());

        var overallStopwatch = Stopwatch.StartNew();
        var totalProcessed = 0;
        var totalSuccess = 0;
        var totalDuplicates = 0;
        var totalFailed = 0;

        try
        {
            if (appConfig.BatchSize > 1)
            {
                logger.Information("Processing users in batches of {BatchSize}", appConfig.BatchSize);

                await foreach (var batch in excelReader.ReadUsersInBatchesAsync(appConfig.ExcelPath, appConfig.BatchSize))
                {
                    logger.Information("Processing batch of {Count} users", batch.Count);

                    var result = await userProcessor.ProcessUsersBatchAsync(batch);

                    totalProcessed += result.TotalRecords;
                    totalSuccess += result.SuccessfulRecords;
                    totalDuplicates += result.DuplicateRecords;
                    totalFailed += result.FailedRecords;

                    logger.Information(
                        "Batch completed. Success: {Success}, Duplicates: {Duplicates}, Failed: {Failed}",
                        result.SuccessfulRecords, result.DuplicateRecords, result.FailedRecords);

                    if (result.Errors.Any())
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.Error("Row {Row} ({Email}): {Error}",
                                error.ExcelRow, error.Email, error.ErrorMessage);
                        }
                    }
                }
            }
            else
            {
                logger.Information("Processing all users at once");

                var users = await excelReader.ReadUsersFromExcelAsync(appConfig.ExcelPath);

                if (!users.Any())
                {
                    logger.Warning("No valid users found in Excel file");
                    return;
                }

                var result = await userProcessor.ProcessUsersAsync(users);

                totalProcessed = result.TotalRecords;
                totalSuccess = result.SuccessfulRecords;
                totalDuplicates = result.DuplicateRecords;
                totalFailed = result.FailedRecords;

                if (result.Errors.Any())
                {
                    foreach (var error in result.Errors)
                    {
                        logger.Error("Row {Row} ({Email}): {Error}",
                            error.ExcelRow, error.Email, error.ErrorMessage);
                    }
                }
            }

            overallStopwatch.Stop();

            // Log summary
            logger.Information("=== Processing Summary ===");
            logger.Information("Total Processing Time: {Duration}", overallStopwatch.Elapsed);
            logger.Information("Total Records Processed: {Total}", totalProcessed);
            logger.Information("Successful: {Success} ({SuccessRate:P2})",
                totalSuccess, totalProcessed > 0 ? (double)totalSuccess / totalProcessed : 0);
            logger.Information("Duplicates: {Duplicates}", totalDuplicates);
            logger.Information("Failed: {Failed}", totalFailed);
            logger.Information("Duplicate file: {Path}", await duplicateHandler.GetDuplicateFilePath());
        }
        catch (OperationCanceledException)
        {
            logger.Warning("Operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unexpected error during processing");
            throw;
        }
    }

    private static string GetProjectRoot()
    {
        var basePath = AppContext.BaseDirectory;
        // Use Path.Combine for cross-platform compatibility
        return Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
    }
}
