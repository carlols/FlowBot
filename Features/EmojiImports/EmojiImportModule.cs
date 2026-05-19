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
            await RespondAsync(
                "I found more than one custom emoji in that message. For now, import from a message with exactly one custom emoji.",
                ephemeral: true);
            return;
        }

        var emoji = emojis[0];
        var modal = new ModalBuilder()
            .WithTitle("Import emoji")
            .WithCustomId(EmojiImportIds.CreateModalId(emoji))
            .AddTextInput(
                label: "Emoji name",
                customId: EmojiImportIds.EmojiNameInputId,
                style: TextInputStyle.Short,
                placeholder: "letters, numbers, and underscores only",
                minLength: 2,
                maxLength: 32,
                required: true,
                value: emoji.Name)
            .Build();

        await RespondWithModalAsync(modal);
    }
}
