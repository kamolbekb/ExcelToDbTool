using Microsoft.Extensions.Configuration;
using Npgsql;
using DataInserter;

class Program
{
    static void Main()
    {
        var location = System.Environment.CurrentDirectory;
        var realPath =
            location.Replace(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}net8.0",
                "");

        var builder = new ConfigurationBuilder()
            .SetBasePath(realPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        IConfiguration config = builder.Build();

        string IAMConnectionString = config.GetConnectionString("IAMConnection");
        string SDGConnectionString = config.GetConnectionString("SDGConnection");

        // Get Excel file path from console input
        Console.Write("Enter the full path to the Excel file: ");
        string excelFilePath = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(excelFilePath) || !File.Exists(excelFilePath))
        {
            Console.WriteLine("Invalid file path. Exiting...");
            return;
        }

        List<ExcelUser> users = ExcelUser.ReadUsersFromExcel(excelFilePath);

        if (users.Count == 0)
        {
            Console.WriteLine("No users found in the Excel file. Exiting...");
            return;
        }

        using (var conn1 = new NpgsqlConnection(IAMConnectionString))
        using (var conn2 = new NpgsqlConnection(SDGConnectionString))
        {
            conn1.Open();
            conn2.Open();
            Console.WriteLine("Connected to both databases.");

            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                Guid aspNetUserId;

                using (var transaction1 = conn1.BeginTransaction())
                using (var transaction2 = conn2.BeginTransaction())
                {
                    try
                    {
                        Console.WriteLine($"\nProcessing row {i + 1}: {user.Email}\n");

                        string checkUserQuery = "SELECT COUNT (*) FROM \"AspNetUsers\" WHERE \"Email\" = @Email";

                        using (var checkCmd = new NpgsqlCommand(checkUserQuery, conn1, transaction1))
                        {
                            checkCmd.Parameters.AddWithValue("@Email", user.Email);
                            long userCount = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0);

                            if (userCount == 0)
                            {
                                string insertUserQuery =
                                    "INSERT INTO \"AspNetUsers\" (\"Id\",\"UserName\", \"NormalizedUserName\", \"Email\"," +
                                    "\"NormalizedEmail\",\"PasswordHash\",\"Status\",\"UserType\",\"EmailConfirmed\"," +
                                    "\"PhoneNumberConfirmed\",\"TwoFactorEnabled\",\"LockoutEnabled\",\"AccessFailedCount\",\"IsFromActiveDirectory\")" +
                                    "VALUES (@Id,@UserName, @NormalizedUserName, @Email, @NormalizedEmail,@PasswordHash," +
                                    "@Status,@UserType,@EmailConfirmed,@PhoneNumberConfirmed,@TwoFactorEnabled,@LockoutEnabled,@AccessFailedCount,@IsFromActiveDirectory)";

                                using (var insertCmd = new NpgsqlCommand(insertUserQuery, conn1, transaction1))
                                {
                                    insertCmd.Parameters.AddWithValue("Id", Guid.NewGuid());
                                    insertCmd.Parameters.AddWithValue("UserName", user.Name);
                                    insertCmd.Parameters.AddWithValue("NormalizedUserName", user.Name.ToUpper());
                                    insertCmd.Parameters.AddWithValue("Email", user.Email);
                                    insertCmd.Parameters.AddWithValue("NormalizedEmail", user.Email.ToUpper());
                                    insertCmd.Parameters.AddWithValue("PasswordHash", "nqoO021-i5\\n");

                                    insertCmd.Parameters.AddWithValue("Status", 0);
                                    insertCmd.Parameters.AddWithValue("UserType", 0);
                                    insertCmd.Parameters.AddWithValue("EmailConfirmed", true);
                                    insertCmd.Parameters.AddWithValue("PhoneNumberConfirmed", false);
                                    insertCmd.Parameters.AddWithValue("TwoFactorEnabled", false);
                                    insertCmd.Parameters.AddWithValue("LockoutEnabled", true);
                                    insertCmd.Parameters.AddWithValue("AccessFailedCount", 0);
                                    insertCmd.Parameters.AddWithValue("IsFromActiveDirectory", false);
                                    insertCmd.ExecuteNonQuery();
                                    Console.WriteLine("User created in DB1.");
                                }
                            }

                            string getExistingUser = "SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Email\" = @Email";

                            using (var cmd = new NpgsqlCommand(getExistingUser, conn1, transaction1))
                            {
                                cmd.Parameters.AddWithValue("@Email", user.Email);

                                using (var reader = cmd.ExecuteReader())
                                {
                                    reader.Read();
                                    aspNetUserId = reader.GetGuid(0);
                                }
                            }
                        }

                        string checkDivisionQuery = "SELECT \"Id\" FROM \"Divisions\" WHERE \"Name\" = @Name";
                        int? divisionId = null;

                        using (var checkDivCmd = new NpgsqlCommand(checkDivisionQuery, conn2, transaction2))
                        {
                            checkDivCmd.Parameters.AddWithValue("@Name", user.Devision);
                            var result = checkDivCmd.ExecuteScalar();

                            if (result != null)
                            {
                                divisionId = Convert.ToInt32(result);
                            }
                            else
                            {
                                string insertDivisionQuery =
                                    "INSERT INTO \"Divisions\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";

                                using (var insertDivCmd = new NpgsqlCommand(insertDivisionQuery, conn2, transaction2))
                                {
                                    insertDivCmd.Parameters.AddWithValue("@Name", user.Devision);
                                    divisionId = Convert.ToInt32(insertDivCmd.ExecuteScalar());
                                    Console.WriteLine("Division inserted in DB2.");
                                }
                            }
                        }

                        if (user.Section != null && user.Section != "")
                        {
                            string checkSectionQuery = "SELECT \"Id\" FROM \"Sections\" WHERE \"Name\" = @Name";
                            int? sectionId = null;

                            using (var checkSecCmd = new NpgsqlCommand(checkSectionQuery, conn2, transaction2))
                            {
                                checkSecCmd.Parameters.AddWithValue("@Name", user.Section);
                                var result = checkSecCmd.ExecuteScalar();
                                if (result != null)
                                {
                                    sectionId = Convert.ToInt32(result);
                                }
                                else
                                {
                                    string insertSectionQuery =
                                        "INSERT INTO \"Sections\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";
                                    using (var insertSecCmd =
                                           new NpgsqlCommand(insertSectionQuery, conn2, transaction2))
                                    {
                                        insertSecCmd.Parameters.AddWithValue("@Name", user.Section);
                                        sectionId = Convert.ToInt32(insertSecCmd.ExecuteScalar());
                                        Console.WriteLine("Section inserted in DB2.");
                                    }
                                }
                            }

                            if (sectionId.HasValue && divisionId.HasValue)
                            {
                                string checkSectionDivQuery =
                                    "SELECT COUNT(*) FROM \"SectionDivisions\" WHERE \"SectionId\" = @SectionId AND \"DivisionId\" = @DivisionId";
                                
                                using (var checkSecDivCmd =
                                       new NpgsqlCommand(checkSectionDivQuery, conn2, transaction2))
                                {
                                    checkSecDivCmd.Parameters.AddWithValue("@SectionId", sectionId);
                                    checkSecDivCmd.Parameters.AddWithValue("@DivisionId", divisionId);
                                    long secDivCount = Convert.ToInt64(checkSecDivCmd.ExecuteScalar() ?? 0);

                                    if (secDivCount == 0)
                                    {
                                        string insertSectionDivQuery =
                                            "INSERT INTO \"SectionDivisions\" (\"SectionId\", \"DivisionId\") VALUES (@SectionId, @DivisionId)";
                                        using (var insertSecDivCmd =
                                               new NpgsqlCommand(insertSectionDivQuery, conn2, transaction2))
                                        {
                                            insertSecDivCmd.Parameters.AddWithValue("@SectionId", sectionId);
                                            insertSecDivCmd.Parameters.AddWithValue("@DivisionId", divisionId);
                                            insertSecDivCmd.ExecuteNonQuery();
                                            Console.WriteLine("Section-Division relationship inserted in DB2.");
                                        }
                                    }
                                }
                            }
                        }


                        string checkRoleQuery = "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\" = @Name";
                        int? roleId = null;

                        using (var checkRoleCmd = new NpgsqlCommand(checkRoleQuery, conn2, transaction2))
                        {
                            checkRoleCmd.Parameters.AddWithValue("@Name", user.Role);
                            var result = checkRoleCmd.ExecuteScalar();
                            if (result != null)
                            {
                                roleId = Convert.ToInt32(result);
                            }
                            else
                            {
                                string insertRoleQuery =
                                    "INSERT INTO \"Roles\" (\"Name\", \"Enabled\", \"ApplicationId\") VALUES (@Name, @Enabled, @ApplicationId) RETURNING \"Id\"";
                                using (var insertRoleCmd = new NpgsqlCommand(insertRoleQuery, conn2, transaction2))
                                {
                                    insertRoleCmd.Parameters.AddWithValue("@Name", user.Role);
                                    insertRoleCmd.Parameters.AddWithValue("@Enabled", true);
                                    insertRoleCmd.Parameters.AddWithValue("@ApplicationId", 1);
                                    roleId = Convert.ToInt32(insertRoleCmd.ExecuteScalar());
                                    Console.WriteLine("Role inserted in DB2.");
                                }
                            }
                        }

                        string checkUserGroupQuery = "SELECT \"Id\" FROM \"UserGroups\" WHERE \"Name\" = @Name";
                        int? userGroupId = null;

                        using (var checkUserGroupCmd = new NpgsqlCommand(checkUserGroupQuery, conn2, transaction2))
                        {
                            checkUserGroupCmd.Parameters.AddWithValue("@Name", user.UserGroup);
                            var result = checkUserGroupCmd.ExecuteScalar();
                            if (result != null)
                            {
                                userGroupId = Convert.ToInt32(result);
                            }
                            else
                            {
                                string insertUserGroupQuery =
                                    "INSERT INTO \"UserGroups\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";
                                using (var insertUserGroupCmd =
                                       new NpgsqlCommand(insertUserGroupQuery, conn2, transaction2))
                                {
                                    insertUserGroupCmd.Parameters.AddWithValue("@Name", user.UserGroup);
                                    userGroupId = Convert.ToInt32(insertUserGroupCmd.ExecuteScalar());
                                    Console.WriteLine("UserGroup inserted in DB2.");
                                }
                            }
                        }

                        if (roleId.HasValue && userGroupId.HasValue)
                        {
                            string checkRoleUserGroupQuery =
                                "SELECT COUNT(*) FROM \"RoleUserGroups\" WHERE \"RoleId\" = @RoleId AND \"UserGroupId\" = @UserGroupId";
                            
                            using (var checkRoleUserGroupCmd =
                                   new NpgsqlCommand(checkRoleUserGroupQuery, conn2, transaction2))
                            {
                                checkRoleUserGroupCmd.Parameters.AddWithValue("@RoleId", roleId);
                                checkRoleUserGroupCmd.Parameters.AddWithValue("@UserGroupId", userGroupId);
                                long roleUserGroupCount = Convert.ToInt64(checkRoleUserGroupCmd.ExecuteScalar() ?? 0);

                                if (roleUserGroupCount == 0)
                                {
                                    string insertRoleUserGroupQuery =
                                        "INSERT INTO \"RoleUserGroups\" (\"RoleId\", \"UserGroupId\") VALUES (@RoleId, @UserGroupId)";
                                    using (var insertRoleUserGroupCmd =
                                           new NpgsqlCommand(insertRoleUserGroupQuery, conn2, transaction2))
                                    {
                                        insertRoleUserGroupCmd.Parameters.AddWithValue("@RoleId", roleId);
                                        insertRoleUserGroupCmd.Parameters.AddWithValue("@UserGroupId", userGroupId);
                                        insertRoleUserGroupCmd.ExecuteNonQuery();
                                        Console.WriteLine("Role-UserGroup relationship inserted in DB2.");
                                    }
                                }
                            }
                        }

                        string upsertUserQuery = @"
                                INSERT INTO ""Users"" (""Sub"", ""IsApiAdmin"",""ControlLevel"",""IsTerminated"",""ActorLevel"") 
                                VALUES (@Sub, @IsApiAdmin,@ControlLevel,@IsTerminated,@ActorLevel)
                                ON CONFLICT (""Sub"") 
                                DO UPDATE SET ""IsApiAdmin"" = EXCLUDED.""IsApiAdmin"",
                                    ""ControlLevel"" = EXCLUDED.""ControlLevel"",
                                    ""IsTerminated"" = EXCLUDED.""IsTerminated"",
                                    ""ActorLevel"" = EXCLUDED.""ActorLevel""
                                RETURNING ""Id"";";

                        // int? sdgUserId = null;

                        using (var upsertUserCmd = new NpgsqlCommand(upsertUserQuery, conn2, transaction2))
                        {
                            upsertUserCmd.Parameters.AddWithValue("@Sub", aspNetUserId);
                            upsertUserCmd.Parameters.AddWithValue("@ControlLevel", (int)user.ControlLevel);
                            upsertUserCmd.Parameters.AddWithValue("@IsTerminated", false);
                            upsertUserCmd.Parameters.AddWithValue("@ActorLevel", 1);
                            upsertUserCmd.Parameters.AddWithValue("@IsApiAdmin", 
                                (user.Devision == "ADMINISTRTOR" || user.Devision == "ALL"));    //Here should be changed to ADMINISTRATOR

                            Convert.ToInt32(upsertUserCmd.ExecuteScalar());
                            Console.WriteLine("User upserted in DB2.");
                        }
                        
                        string upsertSubject = @"
                                INSERT INTO ""Subjects"" (""Sub"", ""Name"") 
                                VALUES (@Sub, @Name)
                                ON CONFLICT (""Sub"") 
                                DO UPDATE SET ""Name"" = EXCLUDED.""Name""
                                RETURNING ""Id"";";
                        int? subjectId = null;

                        using (var upsertUserCmd = new NpgsqlCommand(upsertSubject, conn2, transaction2))
                        {
                            upsertUserCmd.Parameters.AddWithValue("@Sub", aspNetUserId);
                            upsertUserCmd.Parameters.AddWithValue("@Name", aspNetUserId);

                            subjectId= Convert.ToInt32(upsertUserCmd.ExecuteScalar());
                            Console.WriteLine("Subjects upserted in DB2.");
                        }
                        
                        if (subjectId.HasValue && userGroupId.HasValue)
                        {
                            string checkRoleUserGroupQuery =
                                "SELECT COUNT(*) FROM \"SubjectUserGroups\" WHERE \"UserGroupId\" = @UserGroupId AND \"SubjectId\" = @SubjectId";
                            
                            using (var checkRoleUserGroupCmd =
                                   new NpgsqlCommand(checkRoleUserGroupQuery, conn2, transaction2))
                            {
                                checkRoleUserGroupCmd.Parameters.AddWithValue("@SubjectId", subjectId);
                                checkRoleUserGroupCmd.Parameters.AddWithValue("@UserGroupId", userGroupId);
                                long roleUserGroupCount = Convert.ToInt64(checkRoleUserGroupCmd.ExecuteScalar() ?? 0);

                                if (roleUserGroupCount == 0)
                                {
                                    string insertSubjectUserGroupQuery =
                                        "INSERT INTO \"SubjectUserGroups\" (\"UserGroupId\",\"SubjectId\") VALUES (@UserGroupId,@SubjectId)";
                                    
                                    using (var insertSubjectUserGroupCmd =
                                           new NpgsqlCommand(insertSubjectUserGroupQuery, conn2, transaction2))
                                    {
                                        insertSubjectUserGroupCmd.Parameters.AddWithValue("@SubjectId", subjectId);
                                        insertSubjectUserGroupCmd.Parameters.AddWithValue("@UserGroupId", userGroupId);
                                        insertSubjectUserGroupCmd.ExecuteNonQuery();
                                        Console.WriteLine("Subject-UserGroup relationship inserted in DB2.");
                                    }
                                }
                            }
                        }

                        transaction1.Commit();
                        transaction2.Commit();

                        Console.WriteLine($"\nUser: {user.Name} Inserted Successfully.\n--------------------------------------");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing row {i + 1}: {ex.Message}");
                        Console.WriteLine($"Rolling back the current {i + 1}st row and skipping to the next...");

                        transaction1.Rollback();
                        transaction2.Rollback();

                        if (ex is NpgsqlException)
                        {
                            Console.WriteLine("Database connection error. Stopping processing.");
                            break;
                        }
                    }
                }
            }
        }

        Console.WriteLine("\nProcessing complete.");
    }
}