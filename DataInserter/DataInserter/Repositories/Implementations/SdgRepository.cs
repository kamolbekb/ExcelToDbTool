using DataInserter.Constants;
using DataInserter.Models;
using DataInserter.Repositories.Interfaces;
using DataInserter.Utilities;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

namespace DataInserter.Repositories.Implementations;

public class SdgRepository : ISdgRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly Dictionary<string, int> _divisionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _sectionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _roleCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _userGroupCache = new(StringComparer.OrdinalIgnoreCase);

    public SdgRepository(IConfiguration configuration, ILogger logger)
    {
        _connectionString = configuration.GetConnectionString("SDGConnection") 
            ?? throw new ArgumentException("SDGConnection not found in configuration");
        _logger = logger.ForContext<SdgRepository>();
    }

    #region Division Operations

    public async Task<int> GetOrCreateDivisionAsync(string divisionName, CancellationToken cancellationToken = default)
    {
        if (_divisionCache.TryGetValue(divisionName, out var cachedId))
            return cachedId;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if exists
        const string checkQuery = "SELECT \"Id\" FROM \"Divisions\" WHERE \"Name\" = @Name";
        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        checkCommand.Parameters.AddWithValue("@Name", divisionName);

        var result = await checkCommand.ExecuteScalarAsync(cancellationToken);
        if (result != null)
        {
            var id = Convert.ToInt32(result);
            _divisionCache[divisionName] = id;
            return id;
        }

        // Insert new
        const string insertQuery = "INSERT INTO \"Divisions\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";
        await using var insertCommand = new NpgsqlCommand(insertQuery, connection);
        insertCommand.Parameters.AddWithValue("@Name", divisionName);

        var newId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
        _divisionCache[divisionName] = newId;
        _logger.Information("Division inserted in SDGDB.");
        
        return newId;
    }

    public async Task<Dictionary<string, int>> GetExistingDivisionsAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT \"Id\", \"Name\" FROM \"Divisions\"";
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var divisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            divisions[name] = id;
            _divisionCache[name] = id; // Update cache
        }

        return divisions;
    }

    #endregion

    #region Section Operations

    public async Task<int?> GetOrCreateSectionAsync(string sectionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
            return null;

        if (_sectionCache.TryGetValue(sectionName, out var cachedId))
            return cachedId;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if exists
        const string checkQuery = "SELECT \"Id\" FROM \"Sections\" WHERE \"Name\" = @Name";
        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        checkCommand.Parameters.AddWithValue("@Name", sectionName);

        var result = await checkCommand.ExecuteScalarAsync(cancellationToken);
        if (result != null)
        {
            var id = Convert.ToInt32(result);
            _sectionCache[sectionName] = id;
            return id;
        }

        // Insert new
        const string insertQuery = "INSERT INTO \"Sections\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";
        await using var insertCommand = new NpgsqlCommand(insertQuery, connection);
        insertCommand.Parameters.AddWithValue("@Name", sectionName);

        var newId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
        _sectionCache[sectionName] = newId;
        _logger.Information("Section inserted in SDGDB.");
        
        return newId;
    }

    public async Task<Dictionary<string, int>> GetExistingSectionsAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT \"Id\", \"Name\" FROM \"Sections\"";
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var sections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            sections[name] = id;
            _sectionCache[name] = id; // Update cache
        }

        return sections;
    }

    #endregion

    #region Role Operations

    public async Task<int> GetOrCreateRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var mappedRoleName = RoleMapper.MapRole(roleName);
        
        if (_roleCache.TryGetValue(mappedRoleName, out var cachedId))
            return cachedId;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if exists
        const string checkQuery = "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\" = @Name";
        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        checkCommand.Parameters.AddWithValue("@Name", mappedRoleName);

        var result = await checkCommand.ExecuteScalarAsync(cancellationToken);
        if (result != null)
        {
            var id = Convert.ToInt32(result);
            _roleCache[mappedRoleName] = id;
            return id;
        }

        // Insert new
        const string insertQuery = @"
            INSERT INTO ""Roles"" (""Name"", ""Enabled"", ""ApplicationId"") 
            VALUES (@Name, @Enabled, @ApplicationId) 
            RETURNING ""Id""";
            
        await using var insertCommand = new NpgsqlCommand(insertQuery, connection);
        insertCommand.Parameters.AddWithValue("@Name", mappedRoleName);
        insertCommand.Parameters.AddWithValue("@Enabled", true);
        insertCommand.Parameters.AddWithValue("@ApplicationId", ApplicationConstants.ApplicationId.Default);

        var newId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
        _roleCache[mappedRoleName] = newId;
        _logger.Information("Role '{RoleName}' inserted in SDGDB.", mappedRoleName);
        
        return newId;
    }

    public async Task<Dictionary<string, int>> GetExistingRolesAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT \"Id\", \"Name\" FROM \"Roles\"";
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var roles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1).Trim();
            roles[name] = id;
            _roleCache[name] = id; // Update cache
        }

        return roles;
    }

    #endregion

    #region UserGroup Operations

    public async Task<int> GetOrCreateUserGroupAsync(string userGroupName, CancellationToken cancellationToken = default)
    {
        var mappedUserGroupName = RoleMapper.MapUserGroup(userGroupName);
        
        if (_userGroupCache.TryGetValue(mappedUserGroupName, out var cachedId))
            return cachedId;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if exists
        const string checkQuery = "SELECT \"Id\" FROM \"UserGroups\" WHERE \"Name\" = @Name";
        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        checkCommand.Parameters.AddWithValue("@Name", mappedUserGroupName);

        var result = await checkCommand.ExecuteScalarAsync(cancellationToken);
        if (result != null)
        {
            var id = Convert.ToInt32(result);
            _userGroupCache[mappedUserGroupName] = id;
            return id;
        }

        // Insert new
        const string insertQuery = "INSERT INTO \"UserGroups\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";
        await using var insertCommand = new NpgsqlCommand(insertQuery, connection);
        insertCommand.Parameters.AddWithValue("@Name", mappedUserGroupName);

        var newId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
        _userGroupCache[mappedUserGroupName] = newId;
        _logger.Information("UserGroup '{UserGroupName}' inserted in SDGDB.", mappedUserGroupName);
        
        return newId;
    }

    public async Task<Dictionary<string, int>> GetExistingUserGroupsAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT \"Id\", \"Name\" FROM \"UserGroups\"";
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var userGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1).Trim();
            userGroups[name] = id;
            _userGroupCache[name] = id; // Update cache
        }

        return userGroups;
    }

    #endregion

    #region User Operations

    public async Task<int> UpsertUserAsync(Guid aspNetUserId, ExcelUser excelUser, CancellationToken cancellationToken = default)
    {
        const string query = @"
            INSERT INTO ""Users"" (""Sub"", ""IsApiAdmin"", ""ControlLevel"", ""IsTerminated"", ""ActorLevel"") 
            VALUES (@Sub, @IsApiAdmin, @ControlLevel, @IsTerminated, @ActorLevel)
            ON CONFLICT (""Sub"") 
            DO UPDATE SET 
                ""IsApiAdmin"" = EXCLUDED.""IsApiAdmin"",
                ""ControlLevel"" = EXCLUDED.""ControlLevel"",
                ""IsTerminated"" = EXCLUDED.""IsTerminated"",
                ""ActorLevel"" = EXCLUDED.""ActorLevel""
            RETURNING ""Id"";";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        
        var isApiAdmin = excelUser.Division.Equals(ApplicationConstants.SpecialDivisions.Administrator, StringComparison.OrdinalIgnoreCase) ||
                        excelUser.Division.Equals(ApplicationConstants.SpecialDivisions.All, StringComparison.OrdinalIgnoreCase);
        
        command.Parameters.AddWithValue("@Sub", aspNetUserId);
        command.Parameters.AddWithValue("@IsApiAdmin", isApiAdmin);
        command.Parameters.AddWithValue("@ControlLevel", (int)excelUser.ControlLevel);
        command.Parameters.AddWithValue("@IsTerminated", false);
        command.Parameters.AddWithValue("@ActorLevel", ApplicationConstants.ActorLevel.Default);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result!);
    }

    #endregion

    #region Agency Operations

    public async Task<int?> GetDefaultAgencyIdAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT \"Id\" FROM \"Agencies\" ORDER BY \"Id\" LIMIT 1";
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        
        return result != null ? Convert.ToInt32(result) : null;
    }

    #endregion

    #region Relationship Operations

    public async Task CreateSectionDivisionRelationshipAsync(int sectionId, int divisionId, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipAsync(
            "SectionDivisions",
            "SectionId", sectionId,
            "DivisionId", divisionId,
            cancellationToken);
    }

    public async Task CreateRoleUserGroupRelationshipAsync(int roleId, int userGroupId, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipAsync(
            "RoleUserGroups",
            "RoleId", roleId,
            "UserGroupId", userGroupId,
            cancellationToken);
    }

    public async Task CreateUserDivisionRelationshipAsync(int userId, int divisionId, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipAsync(
            "UserDivisions",
            "UserId", userId,
            "DivisionId", divisionId,
            cancellationToken);
    }

    public async Task CreateUserAgencyRelationshipAsync(int userId, int agencyId, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipAsync(
            "UserAgencies",
            "UserId", userId,
            "AgencyId", agencyId,
            cancellationToken);
    }

    public async Task CreateUserSectionRelationshipAsync(int userId, int sectionId, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipAsync(
            "UserSections",
            "UserId", userId,
            "SectionId", sectionId,
            cancellationToken);
    }

    private async Task CreateRelationshipAsync(
        string tableName,
        string column1Name, int column1Value,
        string column2Name, int column2Value,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if relationship exists
        var checkQuery = $@"
            SELECT COUNT(*) FROM ""{tableName}"" 
            WHERE ""{column1Name}"" = @Value1 AND ""{column2Name}"" = @Value2";
            
        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        checkCommand.Parameters.AddWithValue("@Value1", column1Value);
        checkCommand.Parameters.AddWithValue("@Value2", column2Value);

        var count = Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (count > 0)
            return;

        // Insert relationship
        var insertQuery = $@"
            INSERT INTO ""{tableName}"" (""{column1Name}"", ""{column2Name}"") 
            VALUES (@Value1, @Value2)";
            
        await using var insertCommand = new NpgsqlCommand(insertQuery, connection);
        insertCommand.Parameters.AddWithValue("@Value1", column1Value);
        insertCommand.Parameters.AddWithValue("@Value2", column2Value);

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Subject Operations

    public async Task<int> UpsertSubjectAsync(Guid aspNetUserId, CancellationToken cancellationToken = default)
    {
        const string query = @"
            INSERT INTO ""Subjects"" (""Sub"", ""Name"") 
            VALUES (@Sub, @Name)
            ON CONFLICT (""Sub"") 
            DO UPDATE SET ""Name"" = EXCLUDED.""Name""
            RETURNING ""Id"";";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@Sub", aspNetUserId);
        command.Parameters.AddWithValue("@Name", aspNetUserId.ToString());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result!);
    }

    public async Task CreateSubjectUserGroupRelationshipAsync(int subjectId, int userGroupId, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipAsync(
            "SubjectUserGroups",
            "SubjectId", subjectId,
            "UserGroupId", userGroupId,
            cancellationToken);
    }

    #endregion
}
