using Discord;

namespace FlowBot;

public static class RolePanelMessageBuilder
{
    public const int MaxRoles = 25;
    public const string RolesFieldName = "Available roles";

    public static Embed BuildEmbed(RolePanel panel)
    {
        var builder = new EmbedBuilder()
            .WithTitle(panel.Title)
            .WithColor(new Color(88, 101, 242))
            .AddField(
                RolesFieldName,
                string.Join('\n', panel.RoleIds.Select(roleId => $"- <@&{roleId}>")))
            .WithFooter("Manage your choices privately with the button below.");

        if (!string.IsNullOrWhiteSpace(panel.Description))
        {
            builder.WithDescription(panel.Description);
        }

        return builder.Build();
    }

    public static MessageComponent BuildComponents() =>
        new ComponentBuilder()
            .WithButton(
                label: "Manage my roles",
                customId: RolePanelIds.OpenMemberEditor,
                style: ButtonStyle.Primary)
            .Build();
}
