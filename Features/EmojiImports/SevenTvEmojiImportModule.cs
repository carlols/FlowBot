using Discord.Interactions;

namespace FlowBot;

public sealed class SevenTvEmojiImportModule(SevenTvEmojiService sevenTvEmojiService)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("import-7tv-emoji", "Import a 7TV emote into this server.")]
    public async Task ImportSevenTvEmojiAsync(
        [Summary("link", "7TV emote link or ID.")] string link)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("7TV emoji imports can only be used inside a server.", ephemeral: true);
            return;
        }

        if (!EmojiImportPermissions.CanImportEmojis(Context.Guild, Context.User))
        {
            await RespondAsync(EmojiImportPermissions.DeniedMessage, ephemeral: true);
            return;
        }

        if (!SevenTvEmojiLinkParser.TryParseEmoteId(link, out var emoteId))
        {
            await RespondAsync(
                "I could not find a 7TV emote ID in that link. Try a link like `https://7tv.app/emotes/01J0G490ER000396FKBWMCJG8G`.",
                ephemeral: true);
            return;
        }

        var emoji = await sevenTvEmojiService.GetEmojiAsync(emoteId);
        if (emoji is null)
        {
            await RespondAsync(
                "I could not find that 7TV emote, or it does not have a Discord-compatible image file.",
                ephemeral: true);
            return;
        }

        await RespondWithModalAsync(EmojiImportMessageBuilder.CreateSevenTvNameModal(emoji));
    }
}
