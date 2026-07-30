using Discord;
using Discord.WebSocket;

namespace FlowBot;

public static class RoleVoiceMoveMessageBuilder
{
    public static Embed BuildEmbed(
        SocketRole role,
        SocketVoiceChannel destinationChannel,
        SocketVoiceChannel? returnChannel)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Move Role Members")
            .WithDescription("Move connected members with the selected role between voice channels.")
            .AddField("Role", role.Mention, inline: true)
            .AddField("Destination", destinationChannel.Mention, inline: true)
            .WithColor(new Color(163, 113, 247))
            .WithFooter("Only server admins can use these controls.");

        if (returnChannel is not null)
        {
            embed.AddField("Return channel", returnChannel.Mention, inline: true);
        }

        return embed.Build();
    }

    public static MessageComponent BuildComponents(
        SocketRole role,
        SocketVoiceChannel destinationChannel,
        SocketVoiceChannel? returnChannel)
    {
        var components = new ComponentBuilder()
            .WithButton(
                label: "Move to destination",
                customId: RoleVoiceMoveButtonIds.CreateMoveId(role.Id, destinationChannel.Id),
                style: ButtonStyle.Primary);

        if (returnChannel is not null)
        {
            components.WithButton(
                label: "Move to return channel",
                customId: RoleVoiceMoveButtonIds.CreateMoveId(role.Id, returnChannel.Id),
                style: ButtonStyle.Success);
        }

        return components
            .WithButton(
                label: "Close",
                customId: RoleVoiceMoveButtonIds.CreateCloseId(),
                style: ButtonStyle.Secondary)
            .Build();
    }
}
