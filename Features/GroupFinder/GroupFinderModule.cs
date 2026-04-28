using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("group-finder", "Creates a joinable group finder message.")]
    [RequireContext(ContextType.Guild)]
    public async Task CreateGroupFinderAsync(
        [Summary("game-name", "The game or activity you want to play.")] string gameName,
        [Summary("group-size", "The total group size, including you.")] [MinValue(GroupFinderSession.MinCapacity)] [MaxValue(GroupFinderSession.MaxCapacity)] int groupSize,
        [Summary("description", "Optional details about what you want to play.")] string? description = null,
        [Summary("role-to-ping", "Optional server role to ping when posting the message.")] SocketRole? roleToPing = null)
    {
        var session = GroupFinderSession.Create(gameName, description, groupSize, Context.User);
        var embed = GroupFinderMessageBuilder.BuildEmbed(session);
        var components = GroupFinderMessageBuilder.BuildComponents(groupSize, session.PlayerIds.Count);

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
