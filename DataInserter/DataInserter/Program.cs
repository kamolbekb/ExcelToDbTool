using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.RegularExpressions;

namespace DataInserter;

class Program
{
    static void Main()
    {
        var builder = new ConfigurationBuilder();

        string applicationDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..");

        builder.SetBasePath(applicationDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        IConfigurationRoot configuration = builder.Build();

        string IAMConnectionString = configuration.GetConnectionString("IAMConnection");
        string SDGConnectionString = configuration.GetConnectionString("SDGConnection");

        string excelFilePath = configuration.GetSection("ExcelPath").Value;

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

                        string upsertAspNetUserQuery = @"
                            INSERT INTO ""AspNetUsers"" (
                                ""Id"", ""UserName"", ""NormalizedUserName"", ""Email"", ""NormalizedEmail"", ""PasswordHash"", 
                                ""SecurityStamp"", ""ConcurrencyStamp"", ""Status"", ""UserType"", ""EmailConfirmed"", ""PhoneNumberConfirmed"", 
                                ""TwoFactorEnabled"", ""LockoutEnabled"", ""AccessFailedCount"", ""IsFromActiveDirectory""
                                ) VALUES (
                                    @Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @PasswordHash, 
                                    @SecurityStamp, @ConcurrencyStamp, @Status, @UserType, @EmailConfirmed, @PhoneNumberConfirmed, 
                                    @TwoFactorEnabled, @LockoutEnabled, @AccessFailedCount, @IsFromActiveDirectory
                                    )
                            ON CONFLICT (""NormalizedUserName"") DO UPDATE SET 
                                ""Email"" = EXCLUDED.""Email"", 
                                ""NormalizedEmail"" = EXCLUDED.""NormalizedEmail"", 
                                ""PasswordHash"" = EXCLUDED.""PasswordHash"", 
                                ""SecurityStamp"" = EXCLUDED.""SecurityStamp"", 
                                ""ConcurrencyStamp"" = EXCLUDED.""ConcurrencyStamp"" 
                            RETURNING ""Id"";";

                        using (var upsertCmd = new NpgsqlCommand(upsertAspNetUserQuery, conn1, transaction1))
                        {
                            Guid userId = Guid.NewGuid();
                            upsertCmd.Parameters.AddWithValue("Id", userId);
                            upsertCmd.Parameters.AddWithValue("UserName", user.Name);
                            upsertCmd.Parameters.AddWithValue("NormalizedUserName", user.Name.ToUpper());
                            upsertCmd.Parameters.AddWithValue("Email", user.Email);
                            upsertCmd.Parameters.AddWithValue("NormalizedEmail", user.Email.ToUpper());
                            upsertCmd.Parameters.AddWithValue("PasswordHash",
                                "AQAAAAEAACcQAAAAEOJCQJY7OZprpkHjF3TQhjR4SfebNHcCm5BCmFn3YHH2/LmC4xCX93CgxOpx4miA1A==");
                            upsertCmd.Parameters.AddWithValue("SecurityStamp", "TPSRJJCTKHPAOV2GR3HPYS3JR5CFCD5F");
                            upsertCmd.Parameters.AddWithValue("ConcurrencyStamp",
                                "448ad65c-f17e-4eab-b236-1acb563d3e14");
                            upsertCmd.Parameters.AddWithValue("Status", 0);
                            upsertCmd.Parameters.AddWithValue("UserType", 0);
                            upsertCmd.Parameters.AddWithValue("EmailConfirmed", true);
                            upsertCmd.Parameters.AddWithValue("PhoneNumberConfirmed", false);
                            upsertCmd.Parameters.AddWithValue("TwoFactorEnabled", false);
                            upsertCmd.Parameters.AddWithValue("LockoutEnabled", true);
                            upsertCmd.Parameters.AddWithValue("AccessFailedCount", 0);
                            upsertCmd.Parameters.AddWithValue("IsFromActiveDirectory", false);

                            aspNetUserId = (Guid)upsertCmd.ExecuteScalar();
                            Console.WriteLine("User upserted in IAMDB.");
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
                                    Console.WriteLine("Division inserted in SDGDB.");
                                }
                            }
                        }

                        int? sectionId = null;

                        if (user.Section != null && user.Section != "")
                        {
                            string checkSectionQuery = "SELECT \"Id\" FROM \"Sections\" WHERE \"Name\" = @Name";

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
                                        Console.WriteLine("Section inserted in SDGDB.");
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
                                            Console.WriteLine("Section-Division relationship inserted in SDGDB.");
                                        }
                                    }
                                }
                            }
                        }

                        string checkRoleQuery = "SELECT \"Id\", \"Name\" FROM \"Roles\"";
                        var existingRoles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        int? roleId = null;

                        using (var checkRoleCmd = new NpgsqlCommand(checkRoleQuery, conn2, transaction2))
                        using (var reader = checkRoleCmd.ExecuteReader())
                        {
                            while (reader.Read())
                                existingRoles[reader.GetString(1).Trim().ToLower()] = reader.GetInt32(0);
                        }

                        var roleTemplates = new List<string> { "Data Provider", "Data Approver", "Administrator" };

                        string normalizedRole = user.Role.Trim();

                        string matchedRole = roleTemplates
                                                 .FirstOrDefault(template => Regex.IsMatch(normalizedRole,
                                                     $@"\b{template.Split(' ').Last()}\b", RegexOptions.IgnoreCase))
                                             ?? normalizedRole;

                        if (Regex.IsMatch(normalizedRole, @"\d+$"))
                            matchedRole += " " + Regex.Match(normalizedRole, @"\d+$").Value;

                        if (existingRoles.TryGetValue(matchedRole.ToLower(), out int existingRoleId))
                        {
                            roleId = existingRoleId;
                            Console.WriteLine($"Role '{matchedRole}' exists in SDGDB.");
                        }
                        else
                        {
                            string insertRoleQuery =
                                "INSERT INTO \"Roles\" (\"Name\", \"Enabled\", \"ApplicationId\") VALUES (@Name, @Enabled, @ApplicationId) RETURNING \"Id\"";
                            using (var insertRoleCmd = new NpgsqlCommand(insertRoleQuery, conn2, transaction2))
                            {
                                insertRoleCmd.Parameters.AddWithValue("@Name", matchedRole);
                                insertRoleCmd.Parameters.AddWithValue("@Enabled", true);
                                insertRoleCmd.Parameters.AddWithValue("@ApplicationId", 1);
                                roleId = Convert.ToInt32(insertRoleCmd.ExecuteScalar());
                            }

                            Console.WriteLine($"Role '{matchedRole}' inserted in SDGDB.");
                        }


                        string checkUserGroupQuery = "SELECT \"Id\", \"Name\" FROM \"UserGroups\"";
                        var existingUserGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        int? userGroupId = null;

                        using (var checkUserGroupCmd = new NpgsqlCommand(checkUserGroupQuery, conn2, transaction2))
                        using (var reader = checkUserGroupCmd.ExecuteReader())
                        {
                            while (reader.Read())
                                existingUserGroups[reader.GetString(1).Trim().ToLower()] = reader.GetInt32(0);
                        }

                        var userGroupTemplates = new List<string>
                            { "Data Approver Group", "Data Provider Group", "Admin Group" };

                        string normalizedUserGroup = user.UserGroup.Trim();

                        string matchedUserGroup = userGroupTemplates
                                                      .FirstOrDefault(template => Regex.IsMatch(normalizedUserGroup,
                                                          $@"\b{template.Split(' ').ElementAt(1)}\b",
                                                          RegexOptions.IgnoreCase))
                                                  ?? normalizedUserGroup;

                        if (Regex.IsMatch(normalizedUserGroup, @"\d+$"))
                            matchedUserGroup += " " + Regex.Match(normalizedUserGroup, @"\d+$").Value;

                        if (existingUserGroups.TryGetValue(matchedUserGroup.ToLower(), out int existingUserGroupId))
                        {
                            userGroupId = existingUserGroupId;
                            Console.WriteLine($"UserGroup '{matchedUserGroup}' exists in SDGDB.");
                        }
                        else
                        {
                            string insertUserGroupQuery =
                                "INSERT INTO \"UserGroups\" (\"Name\") VALUES (@Name) RETURNING \"Id\"";
                            using (var insertUserGroupCmd =
                                   new NpgsqlCommand(insertUserGroupQuery, conn2, transaction2))
                            {
                                insertUserGroupCmd.Parameters.AddWithValue("@Name", matchedUserGroup);
                                userGroupId = Convert.ToInt32(insertUserGroupCmd.ExecuteScalar());
                            }

                            Console.WriteLine($"UserGroup '{matchedUserGroup}' inserted in SDGDB.");
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
                                        Console.WriteLine("Role-UserGroup relationship inserted in SDGDB.");
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

                        int? sdgUserId = null;

                        using (var upsertUserCmd = new NpgsqlCommand(upsertUserQuery, conn2, transaction2))
                        {
                            upsertUserCmd.Parameters.AddWithValue("@Sub", aspNetUserId);
                            upsertUserCmd.Parameters.AddWithValue("@ControlLevel", (int)user.ControlLevel);
                            upsertUserCmd.Parameters.AddWithValue("@IsTerminated", false);
                            upsertUserCmd.Parameters.AddWithValue("@ActorLevel", 1);
                            upsertUserCmd.Parameters.AddWithValue("@IsApiAdmin",
                                (user.Devision == "ADMINISTRATOR" ||
                                 user.Devision == "ALL")); //Here should be changed to ADMINISTRATOR

                            sdgUserId = Convert.ToInt32(upsertUserCmd.ExecuteScalar());
                            Console.WriteLine("User upserted in SDGDB.");
                        }

                        if (sdgUserId.HasValue && divisionId.HasValue)
                        {
                            string checkUserDivQuery =
                                "SELECT COUNT(*) FROM \"UserDivisions\" WHERE \"UserId\" = @UserId AND \"DivisionId\" = @DivisionId";

                            using (var checkSecDivCmd =
                                   new NpgsqlCommand(checkUserDivQuery, conn2, transaction2))
                            {
                                checkSecDivCmd.Parameters.AddWithValue("@UserId", sdgUserId);
                                checkSecDivCmd.Parameters.AddWithValue("@DivisionId", divisionId);
                                long secDivCount = Convert.ToInt64(checkSecDivCmd.ExecuteScalar() ?? 0);

                                if (secDivCount == 0)
                                {
                                    string insertUserDivQuery =
                                        "INSERT INTO \"UserDivisions\" (\"UserId\", \"DivisionId\") VALUES (@UserId, @DivisionId)";
                                    using (var insertUserDivCmd =
                                           new NpgsqlCommand(insertUserDivQuery, conn2, transaction2))
                                    {
                                        insertUserDivCmd.Parameters.AddWithValue("@UserId", sdgUserId);
                                        insertUserDivCmd.Parameters.AddWithValue("@DivisionId", divisionId);
                                        insertUserDivCmd.ExecuteNonQuery();
                                        Console.WriteLine("User-Division relationship inserted in SDGDB.");
                                    }
                                }
                            }
                        }


                        if (sectionId.HasValue && sdgUserId.HasValue)
                        {
                            string checkSectionDivQuery =
                                "SELECT COUNT(*) FROM \"UserSections\" WHERE \"SectionId\" = @SectionId AND \"UserId\" = @UserId";

                            using (var checkSecDivCmd =
                                   new NpgsqlCommand(checkSectionDivQuery, conn2, transaction2))
                            {
                                checkSecDivCmd.Parameters.AddWithValue("@SectionId", sectionId);
                                checkSecDivCmd.Parameters.AddWithValue("@UserId", sdgUserId);
                                long secDivCount = Convert.ToInt64(checkSecDivCmd.ExecuteScalar() ?? 0);

                                if (secDivCount == 0)
                                {
                                    string insertSectionDivQuery =
                                        "INSERT INTO \"UserSections\" (\"SectionId\", \"UserId\") VALUES (@SectionId, @UserId)";
                                    using (var insertSecDivCmd =
                                           new NpgsqlCommand(insertSectionDivQuery, conn2, transaction2))
                                    {
                                        insertSecDivCmd.Parameters.AddWithValue("@SectionId", sectionId);
                                        insertSecDivCmd.Parameters.AddWithValue("@UserId", sdgUserId);
                                        insertSecDivCmd.ExecuteNonQuery();
                                        Console.WriteLine("User-Section relationship inserted in SDGDB.");
                                    }
                                }
                            }
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

                            subjectId = Convert.ToInt32(upsertUserCmd.ExecuteScalar());
                            Console.WriteLine("Subjects upserted in SDGDB.");
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
                                        Console.WriteLine("Subject-UserGroup relationship inserted in SDGDB.");
                                    }
                                }
                            }
                        }

                        transaction1.Commit();
                        transaction2.Commit();

                        Console.WriteLine(
                            $"\nUser: {user.Name} Inserted Successfully.\n--------------------------------------");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing Excel row {user.ExcelRow}: {ex.Message}");
                        Console.WriteLine(
                            $"Rolling back the current {user.ExcelRow}st Excel row and skipping to the next...");

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