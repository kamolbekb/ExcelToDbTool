using DataInserter.Constants;
using DataInserter.Models;
using System.Text.RegularExpressions;

namespace DataInserter.Utilities;

public static class RoleMapper
{
    public static string MapRole(string inputRole)
    {
        if (string.IsNullOrWhiteSpace(inputRole))
            return inputRole;

        var normalizedRole = inputRole.Trim();

        // Find matching template
        var matchedRole = ApplicationConstants.RoleTemplates.Templates
            .FirstOrDefault(template =>
            {
                var lastWord = template.Split(' ').Last();
                return Regex.IsMatch(normalizedRole, $@"\b{lastWord}\b", RegexOptions.IgnoreCase);
            }) ?? normalizedRole;

        // Append number suffix if exists
        var numberSuffix = StringNormalizer.ExtractNumberSuffix(normalizedRole);
        if (!string.IsNullOrEmpty(numberSuffix))
        {
            matchedRole += " " + numberSuffix;
        }

        return matchedRole;
    }

    public static string MapUserGroup(string inputUserGroup)
    {
        if (string.IsNullOrWhiteSpace(inputUserGroup))
            return inputUserGroup;

        var normalizedUserGroup = inputUserGroup.Trim();

        // Find matching template
        var matchedUserGroup = ApplicationConstants.UserGroupTemplates.Templates
            .FirstOrDefault(template =>
            {
                var words = template.Split(' ');
                if (words.Length >= 2)
                {
                    var secondWord = words[1];
                    return Regex.IsMatch(normalizedUserGroup, $@"\b{secondWord}\b", RegexOptions.IgnoreCase);
                }
                return false;
            }) ?? normalizedUserGroup;

        // Append number suffix if exists
        var numberSuffix = StringNormalizer.ExtractNumberSuffix(normalizedUserGroup);
        if (!string.IsNullOrEmpty(numberSuffix))
        {
            matchedUserGroup += " " + numberSuffix;
        }

        return matchedUserGroup;
    }

    public static ControlLevel ParseControlLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ControlLevel.SECTION;

        var formattedValue = value.ToUpperInvariant();

        if (formattedValue.StartsWith("DEV") || formattedValue.StartsWith("DIV"))
            return ControlLevel.DEVISION;

        if (formattedValue.StartsWith("ORG"))
            return ControlLevel.ORGANIZATION;

        if (formattedValue.StartsWith("APP"))
            return ControlLevel.APPLICATION;

        if (formattedValue.StartsWith("SEC"))
            return ControlLevel.SECTION;

        return Enum.TryParse<ControlLevel>(formattedValue, out var result)
            ? result
            : ControlLevel.SECTION;
    }
}
