using ClosedXML.Excel;

namespace DataInserter;

class ExcelUser
{
    public int ExcelRow { get; set; }
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

            int dataRow = 1;
            foreach (var row in rows.Skip(2)) 
            {
                try
                {
                    var user = new ExcelUser
                    {
                        ExcelRow = row.Cell(1).GetValue<int>(),
                        Name = row.Cell(2).GetString().Trim(),
                        Email = row.Cell(3).GetString().Trim(),
                        Role = row.Cell(4).GetString().Trim(),
                        UserGroup = row.Cell(5).GetString().Trim(),
                        Section = row.Cell(6).GetString().Trim(),
                        Devision = row.Cell(7).GetString().Trim(),
                        ControlLevel = ParseControlLevel("NONE") 

                    };

                    if (!string.IsNullOrWhiteSpace(user.Email))
                    {
                        users.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading row: {ex.Message}");
                    Console.WriteLine($"Excel User on the row: {dataRow}");
                }

                dataRow++;
            }
        }

        return users;
    }
    
    private static ControlLevel ParseControlLevel(string value)
    {
        string formattedValue = value.ToUpper();

        if (formattedValue.StartsWith("DEV") || formattedValue.StartsWith("DIV"))
        {
            return ControlLevel.DEVISION;
        }
        else if (formattedValue.StartsWith("ORG"))
        {
            return ControlLevel.ORGANIZATION;
        }
        else if (formattedValue.StartsWith("APP"))
        {
            return ControlLevel.APPLICATION;
        }
        else if (formattedValue.StartsWith("SEC"))
        {
            return ControlLevel.SECTION;
        }

        return (ControlLevel)Enum.Parse(typeof(ControlLevel), formattedValue);
    }
}
