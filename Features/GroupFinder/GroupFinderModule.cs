using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderModule(GroupFinderTimeParser timeParser) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("group-finder", "Creates a joinable group finder message.")]
    [RequireContext(ContextType.Guild)]
    public async Task CreateGroupFinderAsync(
        [Summary("game-name", "The game or activity you want to play.")] string gameName,
        [Summary("group-size", "Optional total group size, including you. Leave empty for an open-ended group.")] [MinValue(GroupFinderSession.MinCapacity)] [MaxValue(GroupFinderSession.MaxCapacity)] int? groupSize = null,
        [Summary("description", "Optional details about what you want to play.")] string? description = null,
        [Summary("role-to-ping", "Optional server role to ping when posting the message.")] SocketRole? roleToPing = null,
        [Summary("time", "Optional start time, such as 20:00, 17.00, tomorrow 20:00, or 2026-04-28 20:00.")] string? time = null)
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
            session.FullNotificationSent);

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
