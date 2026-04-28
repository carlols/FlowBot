using Discord.Interactions;

namespace FlowBot;

public sealed class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Checks whether FlowBot is awake.")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong!", ephemeral: true);
    }
}
