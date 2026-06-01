using System.Net.Http;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace FlowBot;

public sealed class EmojiImportHandler(
    HttpClient httpClient,
    EmojiImageOptimizer imageOptimizer,
    ILogger<EmojiImportHandler> logger)
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

        if (!EmojiImportPermissions.CanImportEmojis(guild, component.User))
        {
            await component.RespondAsync(EmojiImportPermissions.DeniedMessage, ephemeral: true);
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

        if (!EmojiImportPermissions.CanImportEmojis(guild, modal.User))
        {
            await modal.RespondAsync(EmojiImportPermissions.DeniedMessage, ephemeral: true);
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
            var createdEmoji = await CreateEmoteAsync(guild, emojiName, imageBytes);

            await modal.FollowupAsync($"Imported {createdEmoji} as `:{createdEmoji.Name}:`.", ephemeral: true);
        }
        catch (HttpException exception) when (ShouldTryOptimization(exception))
        {
            if (state.IsAnimated)
            {
                logger.LogInformation(
                    "Skipping optimization for animated emoji {EmojiId} in server {GuildId}.",
                    state.EmojiId,
                    guild.Id);

                await modal.FollowupAsync(
                    "Discord rejected that animated emoji because it could not resize the asset below 256 KB. FlowBot skips animated emoji optimization so the bot can stay online.",
                    ephemeral: true);
                return;
            }

            await TryOptimizeAndImportStaticImageAsync(modal, guild, state, emojiName);
        }
        catch (HttpException exception)
        {
            logger.LogWarning(exception, "Failed to import emoji {EmojiId} into server {GuildId}.", state.EmojiId, guild.Id);
            await modal.FollowupAsync(
                BuildDiscordUploadFailureMessage(exception),
                ephemeral: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Failed to download emoji {EmojiId} from Discord CDN.", state.EmojiId);
            await modal.FollowupAsync(
                "I could not download that emoji from Discord's CDN. It may no longer be available.",
                ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to import emoji {EmojiId} into server {GuildId}.", state.EmojiId, guild.Id);
            await modal.FollowupAsync(
                "I could not import that emoji because an unexpected error occurred.",
                ephemeral: true);
        }
    }

    private async Task TryOptimizeAndImportStaticImageAsync(
        SocketModal modal,
        SocketGuild guild,
        EmojiImportModalState state,
        string emojiName)
    {
        try
        {
            var imageBytes = await httpClient.GetByteArrayAsync(state.CdnUrl);
            var optimizationResult = imageOptimizer.OptimizeStaticImage(imageBytes);

            if (optimizationResult is null)
            {
                await modal.FollowupAsync(
                    "Discord rejected that emoji because it could not resize the asset below 256 KB, and FlowBot could not lightly optimize it enough.",
                    ephemeral: true);
                return;
            }

            var createdEmoji = await CreateEmoteAsync(guild, emojiName, optimizationResult.ImageBytes);

            await modal.FollowupAsync(
                $"Imported {createdEmoji} as `:{createdEmoji.Name}:`. FlowBot lightly optimized it first: {optimizationResult.Description}.",
                ephemeral: true);
        }
        catch (HttpException exception)
        {
            logger.LogWarning(exception, "Failed to import optimized emoji {EmojiId} into server {GuildId}.", state.EmojiId, guild.Id);
            await modal.FollowupAsync(BuildDiscordUploadFailureMessage(exception), ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to optimize and import emoji {EmojiId} into server {GuildId}.", state.EmojiId, guild.Id);
            await modal.FollowupAsync(
                "I could not import that emoji because image optimization failed unexpectedly.",
                ephemeral: true);
        }
    }

    private static async Task<GuildEmote> CreateEmoteAsync(
        SocketGuild guild,
        string emojiName,
        byte[] imageBytes)
    {
        await using var imageStream = new MemoryStream(imageBytes);
        using var image = new Image(imageStream);

        return await guild.CreateEmoteAsync(emojiName, image);
    }

    private static bool ShouldTryOptimization(HttpException exception) =>
        exception.DiscordCode == DiscordErrorCode.FailedToResizeAssetBelowTheMaximumSize;

    private static string BuildDiscordUploadFailureMessage(HttpException exception)
    {
        if (exception.DiscordCode == DiscordErrorCode.FailedToResizeAssetBelowTheMaximumSize)
        {
            return "Discord rejected that emoji because it could not resize the asset below 256 KB.";
        }

        return string.IsNullOrWhiteSpace(exception.Reason)
            ? "Discord rejected that emoji upload."
            : $"Discord rejected that emoji upload: {exception.Reason}";
    }

    private static SocketGuild? GetGuild(SocketModal modal) =>
        (modal.User as SocketGuildUser)?.Guild
        ?? (modal.Channel as SocketGuildChannel)?.Guild;

    private static SocketGuild? GetGuild(SocketMessageComponent component) =>
        (component.User as SocketGuildUser)?.Guild
        ?? (component.Channel as SocketGuildChannel)?.Guild;
}
