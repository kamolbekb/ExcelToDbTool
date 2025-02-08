using ClosedXML.Excel;

namespace DataInserter;

class ExcelUser
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public string UserGroup { get; set; }
    public string Section { get; set; }
    public string Devision { get; set; }
    public ControlLevel ControlLevel { get; set; }
    
    
    public static List<ExcelUser> ReadUsersFromExcel(string filePath)
    {
        var users = new List<ExcelUser>();

        using (var workbook = new XLWorkbook(filePath))
        {
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed().RowsUsed();

            foreach (var row in rows.Skip(2)) // Skip header row
            {
                try
                {
                    var user = new ExcelUser
                    {
                        Name = row.Cell(2).GetString().Trim(),
                        Email = row.Cell(3).GetString().Trim(),
                        Role = row.Cell(4).GetString().Trim(),
                        UserGroup = row.Cell(5).GetString().Trim(),
                        Section = row.Cell(6).GetString().Trim(),
                        Devision = row.Cell(7).GetString().Trim(),
                        ControlLevel = (ControlLevel)Enum.Parse(typeof(ControlLevel),row.Cell(8).GetString().Trim().ToUpper())
                    };

                    if (!string.IsNullOrWhiteSpace(user.Email))
                    {
                        users.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading row: {ex.Message}");
                }
            }
        }

        return users;
    }
}
