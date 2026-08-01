using Discord;
using Discord.WebSocket;

namespace FlowBot;

public static class RoleMessageBuilder
{
    private const int MaxButtonLabelLength = 80;

    public static Embed BuildEmbed(SocketRole role, string title, string message)
    {
        var roleColor = role.Colors.PrimaryColor;

        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(message)
            .AddField("Role", role.Mention, inline: true)
            .WithColor(roleColor.RawValue == 0 ? new Color(88, 101, 242) : roleColor)
            .Build();
    }

    public static MessageComponent BuildComponents(SocketRole role)
    {
        const string prefix = "Add or remove ";
        var availableNameLength = MaxButtonLabelLength - prefix.Length;
        var roleName = role.Name.Length <= availableNameLength
            ? role.Name
            : role.Name[..availableNameLength];

        return new ComponentBuilder()
            .WithButton(
                label: $"{prefix}{roleName}",
                customId: RoleButtonIds.CreateToggleRoleId(role.Id),
                style: ButtonStyle.Primary)
            .Build();
    }
}
