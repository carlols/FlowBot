using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class EmojiImportHandler
{
    private async Task<EmojiImportAsset?> ResolveImportAssetAsync(EmojiImportModalState state)
    {
        if (state.Source == EmojiImportSource.Discord)
        {
            return new EmojiImportAsset(
                state.LogId,
                state.IsAnimated,
                $"https://cdn.discordapp.com/emojis/{state.SourceId}.{(state.IsAnimated ? "gif" : "png")}",
                ConvertToPngBeforeUpload: false);
        }

        var lookupResult = await _sevenTvEmojiService.GetEmojiAsync(state.SourceId);
        if (lookupResult.Asset is not { } sevenTvEmoji)
        {
            return null;
        }

        return new EmojiImportAsset(
            state.LogId,
            sevenTvEmoji.IsAnimated,
            sevenTvEmoji.CdnUrl,
            sevenTvEmoji.ConvertToPngBeforeUpload);
    }

    private async Task<byte[]?> DownloadAndPrepareImageAsync(EmojiImportAsset asset)
    {
        var imageBytes = await _httpClient.GetByteArrayAsync(asset.CdnUrl);

        return asset.ConvertToPngBeforeUpload
            ? _imageOptimizer.ConvertStaticImageToPng(imageBytes)
            : imageBytes;
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

    private static SocketGuild? GetGuild(SocketModal modal) =>
        (modal.User as SocketGuildUser)?.Guild
        ?? (modal.Channel as SocketGuildChannel)?.Guild;

    private static SocketGuild? GetGuild(SocketMessageComponent component) =>
        (component.User as SocketGuildUser)?.Guild
        ?? (component.Channel as SocketGuildChannel)?.Guild;
}
