using DataInserter.Models;

namespace DataInserter.Repositories.Interfaces;

public interface ISdgRepository
{
    // Division operations
    Task<int> GetOrCreateDivisionAsync(string divisionName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetExistingDivisionsAsync(CancellationToken cancellationToken = default);
    
    // Section operations
    Task<int?> GetOrCreateSectionAsync(string sectionName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetExistingSectionsAsync(CancellationToken cancellationToken = default);
    
    // Role operations
    Task<int> GetOrCreateRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetExistingRolesAsync(CancellationToken cancellationToken = default);
    
    // UserGroup operations
    Task<int> GetOrCreateUserGroupAsync(string userGroupName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetExistingUserGroupsAsync(CancellationToken cancellationToken = default);
    
    // User operations
    Task<int> UpsertUserAsync(Guid aspNetUserId, ExcelUser excelUser, CancellationToken cancellationToken = default);
    
    // Agency operations
    Task<int?> GetDefaultAgencyIdAsync(CancellationToken cancellationToken = default);
    
    // Relationship operations
    Task CreateSectionDivisionRelationshipAsync(int sectionId, int divisionId, CancellationToken cancellationToken = default);
    Task CreateRoleUserGroupRelationshipAsync(int roleId, int userGroupId, CancellationToken cancellationToken = default);
    Task CreateUserDivisionRelationshipAsync(int userId, int divisionId, CancellationToken cancellationToken = default);
    Task CreateUserAgencyRelationshipAsync(int userId, int agencyId, CancellationToken cancellationToken = default);
    Task CreateUserSectionRelationshipAsync(int userId, int sectionId, CancellationToken cancellationToken = default);
    
    // Subject operations
    Task<int> UpsertSubjectAsync(Guid aspNetUserId, CancellationToken cancellationToken = default);
    Task CreateSubjectUserGroupRelationshipAsync(int subjectId, int userGroupId, CancellationToken cancellationToken = default);
}
