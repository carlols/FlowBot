using Discord;
using Discord.Interactions;

namespace FlowBot;

public sealed class EmojiImportModule : InteractionModuleBase<SocketInteractionContext>
{
    [MessageCommand("Import Emoji")]
    public async Task ImportEmojiAsync(IMessage message)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("Emoji imports can only be used inside a server.", ephemeral: true);
            return;
        }

        if (Context.User.Id != Context.Guild.OwnerId)
        {
            await RespondAsync("Only the server owner can import emojis with FlowBot.", ephemeral: true);
            return;
        }

        var emojis = EmojiImportParser.FindCustomEmojis(message);

        if (emojis.Count == 0)
        {
            await RespondAsync("I could not find a custom emoji in that message.", ephemeral: true);
            return;
        }

        if (emojis.Count > 1)
        {
            if (emojis.Count > EmojiImportMessageBuilder.MaxSelectableEmojis)
            {
                await RespondAsync(
                    $"I found {emojis.Count} custom emojis in that message, but Discord select menus can only show {EmojiImportMessageBuilder.MaxSelectableEmojis} options. Try a message with fewer custom emojis for now.",
                    ephemeral: true);
                return;
            }

            await RespondAsync(
                "Which emoji do you want to import?",
                components: EmojiImportMessageBuilder.CreateSelectionComponents(emojis),
                ephemeral: true);
            return;
        }

        var emoji = emojis[0];
        await RespondWithModalAsync(EmojiImportMessageBuilder.CreateNameModal(emoji));
    }
}
