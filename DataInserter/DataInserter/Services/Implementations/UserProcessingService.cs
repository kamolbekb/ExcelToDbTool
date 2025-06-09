using DataInserter.Configuration;
using DataInserter.Models;
using DataInserter.Repositories.Interfaces;
using DataInserter.Services.Interfaces;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Retry;
using Serilog;
using System.Diagnostics;

namespace DataInserter.Services.Implementations;

public class UserProcessingService : IUserProcessingService
{
    private readonly IIamRepository _iamRepository;
    private readonly ISdgRepository _sdgRepository;
    private readonly IDuplicateHandlerService _duplicateHandler;
    private readonly UserCommonFields _userCommonFields;
    private readonly ApplicationConfiguration _appConfig;
    private readonly ILogger _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public UserProcessingService(
        IIamRepository iamRepository,
        ISdgRepository sdgRepository,
        IDuplicateHandlerService duplicateHandler,
        IOptions<UserCommonFields> userCommonFields,
        IOptions<ApplicationConfiguration> appConfig,
        ILogger logger)
    {
        _iamRepository = iamRepository;
        _sdgRepository = sdgRepository;
        _duplicateHandler = duplicateHandler;
        _userCommonFields = userCommonFields.Value;
        _appConfig = appConfig.Value;
        _logger = logger.ForContext<UserProcessingService>();

        _retryPolicy = Policy
            .Handle<NpgsqlException>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                _appConfig.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromMilliseconds(_appConfig.RetryDelayMilliseconds * retryAttempt),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.Warning(exception, 
                        "Transient error occurred. Retrying in {Delay}ms (Attempt {RetryCount}/{MaxRetries})",
                        timeSpan.TotalMilliseconds, retryCount, _appConfig.MaxRetryAttempts);
                });
    }

    public async Task<ProcessingResult> ProcessUsersAsync(List<ExcelUser> users, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ProcessingResult
        {
            TotalRecords = users.Count
        };

        _logger.Information("Starting to process users from Excel file...");
        _logger.Information("Total users to process: {Count}", users.Count);

        // Pre-load caches
        await PreloadCachesAsync(cancellationToken);
        _logger.Information("Connected to both databases.\n");
        
        // Get default agency ID once
        var defaultAgencyId = await _sdgRepository.GetDefaultAgencyIdAsync(cancellationToken);
        if (!defaultAgencyId.HasValue)
        {
            _logger.Warning("No default agency found in the system");
        }
        else
        {
            _logger.Information("Using default agency ID: {AgencyId}", defaultAgencyId.Value);
        }

        // Check for existing users in batch
        var emails = users.Select(u => u.Email).Distinct().ToList();
        var existingUsers = await _iamRepository.GetExistingUsersAsync(emails, cancellationToken);

        // Process users
        var processedCount = 0;
        foreach (var user in users)
        {
            processedCount++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.Information("\nProcessing row {Current}/{Total}: {Email}\n", 
                    processedCount, users.Count, user.Email);
                    
                if (existingUsers.ContainsKey(user.Email))
                {
                    result.DuplicateRecords++;
                    await _duplicateHandler.LogDuplicateAsync(new DuplicateRecord
                    {
                        ExcelRow = user.ExcelRow,
                        Email = user.Email,
                        ExistingUserId = existingUsers[user.Email]
                    }, cancellationToken);
                    
                    _logger.Information("Skipping row {Row} because Email: '{Email}' is already in use\n", 
                        processedCount, user.Email);
                    continue;
                }

                await ProcessSingleUserAsync(user, defaultAgencyId, cancellationToken);
                result.SuccessfulRecords++;
                
                _logger.Information("\nUser: {Name} Inserted Successfully.\n--------------------------------------", user.Name);
            }
            catch (Exception ex)
            {
                result.FailedRecords++;
                result.Errors.Add(new ProcessingError
                {
                    ExcelRow = user.ExcelRow,
                    Email = user.Email,
                    ErrorMessage = ex.Message
                });
                
                _logger.Error(ex, "Error processing Excel row {Row}: {Message}", user.ExcelRow, ex.Message);
                _logger.Error("Rolling back the current {Row}st Excel row and skipping to the next...", user.ExcelRow);
            }
        }

        stopwatch.Stop();
        result.ProcessingTime = stopwatch.Elapsed;

        _logger.Information(
            "Processing completed in {Duration}. Success: {Success}, Duplicates: {Duplicates}, Failed: {Failed}",
            result.ProcessingTime, result.SuccessfulRecords, result.DuplicateRecords, result.FailedRecords);
        
        _logger.Information("\nProcessing complete.");

        return result;
    }

    public async Task<ProcessingResult> ProcessUsersBatchAsync(List<ExcelUser> users, CancellationToken cancellationToken = default)
    {
        // For batch processing, we could implement bulk insert operations
        // For now, using the same logic as ProcessUsersAsync
        return await ProcessUsersAsync(users, cancellationToken);
    }

    private async Task ProcessSingleUserAsync(ExcelUser user, int? defaultAgencyId, CancellationToken cancellationToken)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            // Step 1: Create user in IAM database
            var aspNetUserId = await _iamRepository.UpsertUserAsync(user, _userCommonFields, cancellationToken);
            _logger.Information("User upserted in IAMDB.");

            // Step 2: Get or create division
            var divisionId = await _sdgRepository.GetOrCreateDivisionAsync(user.Division, cancellationToken);

            // Step 3: Get or create section (if provided)
            int? sectionId = null;
            if (!string.IsNullOrWhiteSpace(user.Section))
            {
                sectionId = await _sdgRepository.GetOrCreateSectionAsync(user.Section, cancellationToken);
                
                // Create section-division relationship
                if (sectionId.HasValue)
                {
                    await _sdgRepository.CreateSectionDivisionRelationshipAsync(sectionId.Value, divisionId, cancellationToken);
                    _logger.Information("Section-Division relationship inserted in SDGDB.");
                }
            }

            // Step 4: Get or create role
            var roleId = await _sdgRepository.GetOrCreateRoleAsync(user.Role, cancellationToken);

            // Step 5: Get or create user group
            var userGroupId = await _sdgRepository.GetOrCreateUserGroupAsync(user.UserGroup, cancellationToken);

            // Create role-usergroup relationship
            await _sdgRepository.CreateRoleUserGroupRelationshipAsync(roleId, userGroupId, cancellationToken);
            _logger.Information("Role-UserGroup relationship inserted in SDGDB.");

            // Step 6: Create user in SDG database
            var sdgUserId = await _sdgRepository.UpsertUserAsync(aspNetUserId, user, cancellationToken);
            _logger.Information("User upserted in SDGDB.");

            // Step 7: Create user relationships
            await _sdgRepository.CreateUserDivisionRelationshipAsync(sdgUserId, divisionId, cancellationToken);
            _logger.Information("User-Division relationship inserted in SDGDB.");

            if (defaultAgencyId.HasValue)
            {
                await _sdgRepository.CreateUserAgencyRelationshipAsync(sdgUserId, defaultAgencyId.Value, cancellationToken);
                _logger.Information("User-Agency relationship inserted in SDGDB.");
            }

            if (sectionId.HasValue)
            {
                await _sdgRepository.CreateUserSectionRelationshipAsync(sdgUserId, sectionId.Value, cancellationToken);
                _logger.Information("User-Section relationship inserted in SDGDB.");
            }

            // Step 8: Create subject
            var subjectId = await _sdgRepository.UpsertSubjectAsync(aspNetUserId, cancellationToken);
            _logger.Information("Subjects upserted in SDGDB.");
            
            await _sdgRepository.CreateSubjectUserGroupRelationshipAsync(subjectId, userGroupId, cancellationToken);
            _logger.Information("Subject-UserGroup relationship inserted in SDGDB.");
        });
    }

    private async Task PreloadCachesAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Preloading caches...");
        
        var tasks = new List<Task>
        {
            _sdgRepository.GetExistingDivisionsAsync(cancellationToken),
            _sdgRepository.GetExistingSectionsAsync(cancellationToken),
            _sdgRepository.GetExistingRolesAsync(cancellationToken),
            _sdgRepository.GetExistingUserGroupsAsync(cancellationToken)
        };

        await Task.WhenAll(tasks);
        
        _logger.Information("Caches preloaded");
    }

    private static bool IsTransientError(NpgsqlException ex)
    {
        // Identify transient errors that should be retried
        return ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
    }
}
