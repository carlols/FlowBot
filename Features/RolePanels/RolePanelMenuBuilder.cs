using Discord;
using Discord.WebSocket;

namespace FlowBot;

public static class RolePanelMenuBuilder
{
    public static MessageComponent BuildMemberEditor(
        RolePanel panel,
        SocketGuild guild,
        IReadOnlySet<ulong> selectedRoleIds,
        ulong channelId,
        ulong messageId)
    {
        var options = panel.RoleIds
            .Select(guild.GetRole)
            .Where(role => role is not null)
            .Select(role => new SelectMenuOptionBuilder()
                .WithLabel(Truncate(role!.Name, 100))
                .WithValue(role.Id.ToString())
                .WithDefault(selectedRoleIds.Contains(role.Id)))
            .ToList();
        var menu = new SelectMenuBuilder()
            .WithCustomId(RolePanelIds.CreateSaveMemberRolesId(channelId, messageId))
            .WithPlaceholder("Choose the roles you want")
            .WithOptions(options)
            .WithMinValues(0)
            .WithMaxValues(options.Count);

        return new ComponentBuilder()
            .WithSelectMenu(menu)
            .Build();
    }

    public static MessageComponent BuildAdminEditor(
        RolePanel panel,
        SocketGuild guild,
        ulong channelId,
        ulong messageId)
    {
        var addMenu = new SelectMenuBuilder()
            .WithCustomId(RolePanelIds.CreateAddRoleId(channelId, messageId))
            .WithPlaceholder("Add a role to this panel")
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithType(ComponentType.RoleSelect);
        var removeOptions = panel.RoleIds
            .Select(roleId => (RoleId: roleId, Role: guild.GetRole(roleId)))
            .Select(item => new SelectMenuOptionBuilder()
                .WithLabel(Truncate(item.Role?.Name ?? $"Deleted role ({item.RoleId})", 100))
                .WithValue(item.RoleId.ToString()))
            .ToList();
        var removeMenu = new SelectMenuBuilder()
            .WithCustomId(RolePanelIds.CreateRemoveRoleId(channelId, messageId))
            .WithPlaceholder(panel.RoleIds.Count == 1
                ? "A panel must contain at least one role"
                : "Remove a role from this panel")
            .WithOptions(removeOptions)
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithDisabled(panel.RoleIds.Count == 1);

        return new ComponentBuilder()
            .WithSelectMenu(addMenu, row: 0)
            .WithSelectMenu(removeMenu, row: 1)
            .Build();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
