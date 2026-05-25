using Discord;
using Discord.WebSocket;

namespace FlowBot;

public static class RaidVoiceSplitMessageBuilder
{
    public static Embed BuildEmbed(
        SocketRole role,
        SocketVoiceChannel targetChannel,
        SocketVoiceChannel? mainChannel)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Raid Voice Split")
            .WithDescription("Move the configured raid split into its voice channel when the raid lead is ready.")
            .AddField("Role to move", role.Mention, inline: true)
            .AddField("Split voice channel", targetChannel.Mention, inline: true)
            .WithColor(new Color(163, 113, 247))
            .WithFooter("Only server admins can use these controls.");

        if (mainChannel is not null)
        {
            embed.AddField("Main voice channel", mainChannel.Mention, inline: true);
        }

        return embed.Build();
    }

    public static MessageComponent BuildComponents(
        SocketRole role,
        SocketVoiceChannel targetChannel,
        SocketVoiceChannel? mainChannel)
    {
        var components = new ComponentBuilder()
            .WithButton(
                label: "Move to split",
                customId: RaidVoiceSplitButtonIds.CreateMoveGroupId(role.Id, targetChannel.Id),
                style: ButtonStyle.Primary);

        if (mainChannel is not null)
        {
            components.WithButton(
                label: "Move back",
                customId: RaidVoiceSplitButtonIds.CreateMoveBackId(role.Id, mainChannel.Id),
                style: ButtonStyle.Success);
        }

        return components
            .WithButton(
                label: "Close",
                customId: RaidVoiceSplitButtonIds.CreateCloseId(),
                style: ButtonStyle.Secondary)
            .Build();
    }
}
