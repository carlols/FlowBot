using System.Net.Http;
using Discord.Net;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class EmojiImportHandler
{
    private async Task ImportAsync(
        SocketModal modal,
        SocketGuild guild,
        EmojiImportModalState state,
        string emojiName)
    {
        await modal.DeferAsync(ephemeral: true);

        var asset = await ResolveImportAssetAsync(state);
        if (asset is null)
        {
            await modal.FollowupAsync(
                "I could not find that emoji source anymore. The original emote may have been deleted or changed.",
                ephemeral: true);
            return;
        }

        byte[]? imageBytes = null;

        try
        {
            imageBytes = await DownloadAndPrepareImageAsync(asset);
            if (imageBytes is null)
            {
                await modal.FollowupAsync(
                    "I could not prepare that emoji image for upload.",
                    ephemeral: true);
                return;
            }

            var createdEmoji = await CreateEmoteAsync(guild, emojiName, imageBytes);

            await modal.FollowupAsync($"Imported {createdEmoji} as `:{createdEmoji.Name}:`.", ephemeral: true);
        }
        catch (HttpException exception) when (ShouldTryOptimization(exception))
        {
            if (asset.IsAnimated)
            {
                _logger.LogInformation(
                    "Skipping optimization for animated emoji {EmojiId} in server {GuildId}.",
                    asset.LogId,
                    guild.Id);

                await modal.FollowupAsync(
                    "Discord rejected that animated emoji because it could not resize the asset below 256 KB. Flowbot skips animated emoji optimization so the bot can stay online.",
                    ephemeral: true);
                return;
            }

            await TryOptimizeAndImportStaticImageAsync(modal, guild, asset, emojiName, imageBytes!);
        }
        catch (HttpException exception)
        {
            _logger.LogWarning(exception, "Failed to import emoji {EmojiId} into server {GuildId}.", asset.LogId, guild.Id);
            await modal.FollowupAsync(BuildDiscordUploadFailureMessage(exception), ephemeral: true);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Failed to download emoji {EmojiId}.", asset.LogId);
            await modal.FollowupAsync(
                "I could not download that emoji. It may no longer be available.",
                ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to import emoji {EmojiId} into server {GuildId}.", asset.LogId, guild.Id);
            await modal.FollowupAsync(
                "I could not import that emoji because an unexpected error occurred.",
                ephemeral: true);
        }
    }

    private async Task TryOptimizeAndImportStaticImageAsync(
        SocketModal modal,
        SocketGuild guild,
        EmojiImportAsset asset,
        string emojiName,
        byte[] imageBytes)
    {
        try
        {
            var optimizationResult = _imageOptimizer.OptimizeStaticImage(imageBytes);

            if (optimizationResult is null)
            {
                await modal.FollowupAsync(
                    "Discord rejected that emoji because it could not resize the asset below 256 KB, and Flowbot could not lightly optimize it enough.",
                    ephemeral: true);
                return;
            }

            var createdEmoji = await CreateEmoteAsync(guild, emojiName, optimizationResult.ImageBytes);

            await modal.FollowupAsync(
                $"Imported {createdEmoji} as `:{createdEmoji.Name}:`. Flowbot lightly optimized it first: {optimizationResult.Description}.",
                ephemeral: true);
        }
        catch (HttpException exception)
        {
            _logger.LogWarning(exception, "Failed to import optimized emoji {EmojiId} into server {GuildId}.", asset.LogId, guild.Id);
            await modal.FollowupAsync(BuildDiscordUploadFailureMessage(exception), ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to optimize and import emoji {EmojiId} into server {GuildId}.", asset.LogId, guild.Id);
            await modal.FollowupAsync(
                "I could not import that emoji because image optimization failed unexpectedly.",
                ephemeral: true);
        }
    }
}
