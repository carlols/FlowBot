using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class RolePanelMessageParser
{
    public static bool TryParse(IMessage message, out RolePanel panel)
    {
        panel = default!;

        var isRolePanel = message.Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .Any(button => button.CustomId == RolePanelIds.OpenMemberEditor);
        var embed = message.Embeds.FirstOrDefault();

        if (!isRolePanel || embed is null || string.IsNullOrWhiteSpace(embed.Title))
        {
            return false;
        }

        var rolesValue = embed.Fields
            .FirstOrDefault(field => field.Name == RolePanelMessageBuilder.RolesFieldName)
            .Value;
        var roleIds = RoleMentionRegex()
            .Matches(rolesValue ?? string.Empty)
            .Select(match => ulong.TryParse(match.Groups["id"].Value, out var roleId) ? roleId : 0)
            .Where(roleId => roleId != 0)
            .Distinct()
            .ToArray();

        if (roleIds.Length == 0)
        {
            return false;
        }

        panel = new RolePanel(embed.Title, embed.Description, roleIds);
        return true;
    }

    [GeneratedRegex("<@&(?<id>\\d+)>")]
    private static partial Regex RoleMentionRegex();
}
