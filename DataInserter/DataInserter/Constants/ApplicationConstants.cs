namespace DataInserter.Constants;

public static class ApplicationConstants
{
    public static class UserStatus
    {
        public const int Active = 0;
        public const int Inactive = 1;
    }

    public static class UserType
    {
        public const int Regular = 0;
        public const int Admin = 1;
    }

    public static class ActorLevel
    {
        public const int Default = 1;
    }

    public static class ApplicationId
    {
        public const int Default = 1;
    }

    // Commented out as roles are no longer used in the new structure
    // public static class RoleTemplates
    // {
    //     public static readonly List<string> Templates = new()
    //     {
    //         "Data Provider",
    //         "Data Approver",
    //         "Administrator"
    //     };
    // }

    public static class UserGroupTemplates
    {
        public static readonly List<string> Templates = new()
        {
            "Data Provider Group",
            "Data Approver Group",
            "Admin Group"
        };
    }

    public static class SpecialDivisions
    {
        public const string Administrator = "ADMINISTRATOR";
        public const string All = "ALL";
    }

    public static class FileExtensions
    {
        public const string Excel = ".xlsx";
        public const string ExcelOld = ".xls";
    }

    public static class LogFilePatterns
    {
        public const string DataInserterLog = "DataInserterLog_{0}.txt";
        public const string DuplicatesLog = "duplicates_{0}.txt";
    }

    public static class DirectoryNames
    {
        public const string Logs = "Logs";
        public const string DuplicateRecords = "DuplicateRecords";
    }
}
