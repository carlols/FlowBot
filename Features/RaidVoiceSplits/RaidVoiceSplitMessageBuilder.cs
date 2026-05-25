using Discord;
using Discord.WebSocket;

namespace FlowBot;

public static class RaidVoiceSplitMessageBuilder
{
    public static Embed BuildEmbed(SocketRole role, SocketVoiceChannel targetChannel) =>
        new EmbedBuilder()
            .WithTitle("Raid Voice Split")
            .WithDescription("Move the configured raid split into its voice channel when the raid lead is ready.")
            .AddField("Role to move", role.Mention, inline: true)
            .AddField("Target voice channel", targetChannel.Mention, inline: true)
            .WithColor(new Color(163, 113, 247))
            .WithFooter("Only server admins can use these controls.")
            .Build();

    public static MessageComponent BuildComponents(SocketRole role, SocketVoiceChannel targetChannel) =>
        new ComponentBuilder()
            .WithButton(
                label: "Move group",
                customId: RaidVoiceSplitButtonIds.CreateMoveGroupId(role.Id, targetChannel.Id),
                style: ButtonStyle.Primary)
            .WithButton(
                label: "Close",
                customId: RaidVoiceSplitButtonIds.CreateCloseId(),
                style: ButtonStyle.Secondary)
            .Build();
}
