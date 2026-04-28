using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class GroupFinderMessageBuilder
{
    public const string StatusFieldName = "Group";
    public const string PlayersFieldName = "Players";

    public static Embed BuildEmbed(GroupFinderSession session)
    {
        return new EmbedBuilder()
            .WithTitle(session.GameName)
            .WithDescription(session.Description ?? "Looking for players.")
            .AddField(StatusFieldName, $"{session.PlayerIds.Count}/{session.Capacity} people in group", inline: true)
            .AddField(PlayersFieldName, FormatPlayers(session.PlayerIds))
            .WithColor(new Color(87, 242, 135))
            .WithFooter("Use the buttons below to join or leave this group.")
            .Build();
    }

    public static MessageComponent BuildComponents(int capacity, int playerCount)
    {
        return new ComponentBuilder()
            .WithButton(
                label: "Join group",
                customId: GroupFinderButtonIds.CreateJoinId(capacity),
                style: ButtonStyle.Success,
                disabled: playerCount >= capacity)
            .WithButton(
                label: "Leave group",
                customId: GroupFinderButtonIds.CreateLeaveId(capacity),
                style: ButtonStyle.Danger)
            .Build();
    }

    public static bool TryReadSession(IMessage message, int capacity, out GroupFinderSession session)
    {
        session = new GroupFinderSession("Unknown game", null, capacity, []);

        var embed = message.Embeds.FirstOrDefault();

        if (embed is null)
        {
            return false;
        }

        var playersField = embed.Fields.FirstOrDefault(field => field.Name == PlayersFieldName);
        var playerIds = PlayerMentionRegex()
            .Matches(playersField.Value ?? string.Empty)
            .Select(match => ulong.Parse(match.Groups["id"].Value))
            .Distinct()
            .ToArray();

        session = new GroupFinderSession(embed.Title, embed.Description, capacity, playerIds);
        return true;
    }

    private static string FormatPlayers(IReadOnlyCollection<ulong> playerIds)
    {
        if (playerIds.Count == 0)
        {
            return "No players yet.";
        }

        return string.Join(Environment.NewLine, playerIds.Select((playerId, index) => $"{index + 1}. <@{playerId}>"));
    }

    [GeneratedRegex("<@!?(?<id>\\d+)>")]
    private static partial Regex PlayerMentionRegex();
}
