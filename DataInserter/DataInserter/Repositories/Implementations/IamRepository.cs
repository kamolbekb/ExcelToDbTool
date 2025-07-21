using DataInserter.Constants;
using DataInserter.Models;
using DataInserter.Repositories.Interfaces;
using DataInserter.Utilities;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

namespace DataInserter.Repositories.Implementations;

public class IamRepository : IIamRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public IamRepository(IConfiguration configuration, ILogger logger)
    {
        _connectionString = configuration.GetConnectionString("IAMConnection")
            ?? throw new ArgumentException("IAMConnection not found in configuration");
        _logger = logger.ForContext<IamRepository>();
    }

    public async Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string query = "SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Email\" = @Email";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@Email", email);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Guid.Parse(result.ToString()!) : null;
    }

    public async Task<bool> UserExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var userId = await GetUserIdByEmailAsync(email, cancellationToken);
        return userId.HasValue;
    }

    public async Task<Dictionary<string, Guid>> GetExistingUsersAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        var emailList = emails.ToList();
        if (!emailList.Any())
            return new Dictionary<string, Guid>();

        const string query = "SELECT \"Email\", \"Id\" FROM \"AspNetUsers\" WHERE \"Email\" = ANY(@Emails)";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@Emails", emailList.ToArray());

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var email = reader.GetString(0);
            var id = reader.GetGuid(1);
            result[email] = id;
        }

        return result;
    }

    public async Task<Guid> UpsertUserAsync(ExcelUser user, UserCommonFields commonFields, CancellationToken cancellationToken = default)
    {
        const string query = @"
            INSERT INTO ""AspNetUsers"" (
                ""Id"",""FullName"",""UserName"", ""NormalizedUserName"", ""Email"", ""NormalizedEmail"", 
                ""PasswordHash"", ""SecurityStamp"", ""ConcurrencyStamp"", ""Status"", ""UserType"", 
                ""EmailConfirmed"", ""PhoneNumberConfirmed"", ""TwoFactorEnabled"", ""LockoutEnabled"", 
                ""AccessFailedCount"", ""IsFromActiveDirectory""
            ) VALUES (
                @Id, @UserName, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, 
                @PasswordHash, @SecurityStamp, @ConcurrencyStamp, @Status, @UserType, 
                @EmailConfirmed, @PhoneNumberConfirmed, @TwoFactorEnabled, @LockoutEnabled, 
                @AccessFailedCount, @IsFromActiveDirectory
            )
            ON CONFLICT (""NormalizedUserName"") DO UPDATE SET 
                ""Email"" = EXCLUDED.""Email"", 
                ""NormalizedEmail"" = EXCLUDED.""NormalizedEmail"", 
                ""PasswordHash"" = EXCLUDED.""PasswordHash"", 
                ""SecurityStamp"" = EXCLUDED.""SecurityStamp"", 
                ""ConcurrencyStamp"" = EXCLUDED.""ConcurrencyStamp"" 
            RETURNING ""Id"";";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);

        var userId = Guid.NewGuid();
        command.Parameters.AddWithValue("@Id", userId);
        command.Parameters.AddWithValue("@UserName", user.Name);
        command.Parameters.AddWithValue("@NormalizedUserName", StringNormalizer.NormalizeUserName(user.Name));
        command.Parameters.AddWithValue("@Email", user.Email);
        command.Parameters.AddWithValue("@NormalizedEmail", StringNormalizer.NormalizeEmail(user.Email));
        command.Parameters.AddWithValue("@PasswordHash", commonFields.PasswordHash);
        command.Parameters.AddWithValue("@SecurityStamp", commonFields.SecurityStamp);
        command.Parameters.AddWithValue("@ConcurrencyStamp", commonFields.ConcurrencyStamp);
        command.Parameters.AddWithValue("@Status", ApplicationConstants.UserStatus.Active);
        command.Parameters.AddWithValue("@UserType", ApplicationConstants.UserType.Regular);
        command.Parameters.AddWithValue("@EmailConfirmed", true);
        command.Parameters.AddWithValue("@PhoneNumberConfirmed", false);
        command.Parameters.AddWithValue("@TwoFactorEnabled", false);
        command.Parameters.AddWithValue("@LockoutEnabled", true);
        command.Parameters.AddWithValue("@AccessFailedCount", 0);
        command.Parameters.AddWithValue("@IsFromActiveDirectory", false);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Guid.Parse(result.ToString()!) : userId;
    }
}
