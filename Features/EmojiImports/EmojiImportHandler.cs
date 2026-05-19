using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class EmojiImportHandler(HttpClient httpClient, ILogger<EmojiImportHandler> logger)
{
    public async Task HandleComponentAsync(SocketMessageComponent component)
    {
        if (component.Data.CustomId != EmojiImportIds.EmojiSelectId)
        {
            await component.RespondAsync("I could not understand that emoji import selection.", ephemeral: true);
            return;
        }

        var guild = GetGuild(component);
        if (guild is null)
        {
            await component.RespondAsync("Emoji imports can only be completed inside a server.", ephemeral: true);
            return;
        }

        if (component.User.Id != guild.OwnerId)
        {
            await component.RespondAsync("Only the server owner can import emojis with FlowBot.", ephemeral: true);
            return;
        }

        var selectedValue = component.Data.Values.FirstOrDefault();
        if (selectedValue is null || !EmojiImportIds.TryParseSelectValue(selectedValue, out var emoji))
        {
            await component.RespondAsync("I could not understand that emoji selection.", ephemeral: true);
            return;
        }

        await component.RespondWithModalAsync(EmojiImportMessageBuilder.CreateNameModal(emoji));
    }

    public async Task HandleModalAsync(SocketModal modal)
    {
        if (!EmojiImportIds.TryParseModal(modal.Data.CustomId, out var state))
        {
            await modal.RespondAsync("I could not understand that emoji import request.", ephemeral: true);
            return;
        }

        var guild = GetGuild(modal);
        if (guild is null)
        {
            await modal.RespondAsync("Emoji imports can only be completed inside a server.", ephemeral: true);
            return;
        }

        if (modal.User.Id != guild.OwnerId)
        {
            await modal.RespondAsync("Only the server owner can import emojis with FlowBot.", ephemeral: true);
            return;
        }

        if (guild.CurrentUser is not null
            && !guild.CurrentUser.GuildPermissions.ManageEmojisAndStickers
            && !guild.CurrentUser.GuildPermissions.CreateGuildExpressions
            && !guild.CurrentUser.GuildPermissions.Administrator)
        {
            await modal.RespondAsync(
                "FlowBot needs the `Manage Emojis and Stickers` permission to import emojis.",
                ephemeral: true);
            return;
        }

        var emojiName = EmojiImportName.Normalize(
            modal.Data.Components
                .FirstOrDefault(component => component.CustomId == EmojiImportIds.EmojiNameInputId)
                ?.Value ?? string.Empty);

        if (!EmojiImportName.IsValid(emojiName))
        {
            await modal.RespondAsync(
                "Emoji names must be 2-32 characters long and can only use letters, numbers, and underscores.",
                ephemeral: true);
            return;
        }

        if (guild.Emotes.Any(emote => string.Equals(emote.Name, emojiName, StringComparison.OrdinalIgnoreCase)))
        {
            await modal.RespondAsync($"This server already has an emoji named `:{emojiName}:`.", ephemeral: true);
            return;
        }

        await modal.DeferAsync(ephemeral: true);

        try
        {
            var imageBytes = await httpClient.GetByteArrayAsync(state.CdnUrl);
            await using var imageStream = new MemoryStream(imageBytes);
            using var image = new Image(imageStream);

            var createdEmoji = await guild.CreateEmoteAsync(emojiName, image);

            await modal.FollowupAsync($"Imported {createdEmoji} as `:{createdEmoji.Name}:`.", ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to import emoji {EmojiId} into server {GuildId}.", state.EmojiId, guild.Id);
            await modal.FollowupAsync(
                "I could not import that emoji. The server may be out of emoji slots, the image may be too large, or Discord rejected the upload.",
                ephemeral: true);
        }
    }

    private static SocketGuild? GetGuild(SocketModal modal) =>
        (modal.User as SocketGuildUser)?.Guild
        ?? (modal.Channel as SocketGuildChannel)?.Guild;

    private static SocketGuild? GetGuild(SocketMessageComponent component) =>
        (component.User as SocketGuildUser)?.Guild
        ?? (component.Channel as SocketGuildChannel)?.Guild;
}
