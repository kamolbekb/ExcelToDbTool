namespace DataInserter.Models;

public class ExcelUser
{
    public int ExcelRow { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string UserGroup { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public ControlLevel ControlLevel { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Email) && 
               !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(Division);
    }

    public string GetNormalizedEmail() => Email.Trim().ToUpperInvariant();
    public string GetNormalizedName() => Name.Trim().ToUpperInvariant();
}
