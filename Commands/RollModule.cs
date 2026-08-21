using System.Security.Cryptography;
using Discord;
using Discord.Interactions;

namespace FlowBot;

public sealed class RollModule : InteractionModuleBase<SocketInteractionContext>
{
    private const int DefaultMaximum = 100;

    [SlashCommand("roll", "Roll a random number between 1 and a chosen maximum.")]
    public async Task RollAsync(
        [Summary("maximum", "Highest possible roll. Defaults to 100.")]
        [MinValue(1)]
        [MaxValue(int.MaxValue)]
        int maximum = DefaultMaximum)
    {
        if (maximum < 1)
        {
            await RespondAsync("The maximum roll must be at least 1.", ephemeral: true);
            return;
        }

        var result = RandomNumberGenerator.GetInt32(maximum) + 1;

        await RespondAsync(
            $"{Context.User.Mention} rolled **{result}** (1-{maximum}).",
            allowedMentions: new AllowedMentions { UserIds = [Context.User.Id] });
    }
}