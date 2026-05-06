using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderModule(GroupFinderTimeParser timeParser) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("group-finder", "Create a group that people can join with buttons.")]
    [RequireContext(ContextType.Guild)]
    public async Task CreateGroupFinderAsync(
        [Summary("game-name", "Game or activity name shown at the top of the group message.")] string gameName,
        [Summary("group-size", "Max players, including you. Leave empty if anyone can join.")] [MinValue(GroupFinderSession.MinCapacity)] [MaxValue(GroupFinderSession.MaxCapacity)] int? groupSize = null,
        [Summary("description", "Short note about what you want to do.")] string? description = null,
        [Summary("role-to-ping", "Role to notify when the group is posted.")] SocketRole? roleToPing = null,
        [Summary("time", "Start time, like 20:00, 17.00, tomorrow 20:00, or 2026-04-28 20:00.")] string? time = null)
    {
        if (!timeParser.TryParse(time, out var startsAtUnixTimeSeconds, out var errorMessage))
        {
            await RespondAsync(errorMessage, ephemeral: true);
            return;
        }

        var session = GroupFinderSession.Create(gameName, description, groupSize, Context.User, startsAtUnixTimeSeconds);
        var embed = GroupFinderMessageBuilder.BuildEmbed(session);
        var components = GroupFinderMessageBuilder.BuildComponents(
            groupSize,
            session.PlayerIds.Count,
            session.CapacityNoticeSent,
            session.SessionStarted);

        await RespondAsync("Group finder created.", ephemeral: true);

        await Context.Channel.SendMessageAsync(
            text: roleToPing?.Mention,
            embed: embed,
            components: components,
            allowedMentions: roleToPing is null
                ? AllowedMentions.None
                : new AllowedMentions { RoleIds = [roleToPing.Id] });
    }
}
