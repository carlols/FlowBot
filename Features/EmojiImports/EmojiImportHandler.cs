using Discord.WebSocket;

namespace FlowBot;

public sealed partial class EmojiImportHandler
{
    private readonly HttpClient _httpClient;
    private readonly SevenTvEmojiService _sevenTvEmojiService;
    private readonly EmojiImageOptimizer _imageOptimizer;
    private readonly ILogger<EmojiImportHandler> _logger;

    public EmojiImportHandler(
        HttpClient httpClient,
        SevenTvEmojiService sevenTvEmojiService,
        EmojiImageOptimizer imageOptimizer,
        ILogger<EmojiImportHandler> logger)
    {
        _httpClient = httpClient;
        _sevenTvEmojiService = sevenTvEmojiService;
        _imageOptimizer = imageOptimizer;
        _logger = logger;
    }

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

        if (!CanManageEmojis(guild))
        {
            await modal.RespondAsync(
                "Flowbot needs the `Manage Emojis and Stickers` permission to import emojis.",
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

        await ImportAsync(modal, guild, state, emojiName);
    }

    private static bool CanManageEmojis(SocketGuild guild)
    {
        var currentUser = guild.CurrentUser;

        return currentUser is null
            || currentUser.GuildPermissions.ManageEmojisAndStickers
            || currentUser.GuildPermissions.CreateGuildExpressions
            || currentUser.GuildPermissions.Administrator;
    }
}
