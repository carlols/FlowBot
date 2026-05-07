using Discord;
using Discord.Interactions;

namespace FlowBot;

public sealed class PullCountGuessModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("guess-pull-count", "Start a boss pull-count guessing board.")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CreatePullCountGuessBoardAsync(
        [Summary("boss-name", "Boss name shown on the guessing board.")] string bossName)
    {
        var session = PullCountGuessSession.Create(bossName);

        await RespondAsync("Pull-count guessing board created.", ephemeral: true);
        await Context.Channel.SendMessageAsync(
            embed: PullCountGuessMessageBuilder.BuildEmbed(session),
            components: PullCountGuessMessageBuilder.BuildComponents(session.IsClosed));
    }
}
