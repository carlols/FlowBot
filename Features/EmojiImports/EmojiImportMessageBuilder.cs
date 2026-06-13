using Discord;

namespace FlowBot;

public static class EmojiImportMessageBuilder
{
    public const int MaxSelectableEmojis = 25;

    public static Modal CreateNameModal(EmojiImportCandidate emoji) =>
        CreateNameModal(EmojiImportIds.CreateModalId(emoji), emoji.Name);

    public static Modal CreateSevenTvNameModal(SevenTvEmojiAsset emoji) =>
        CreateNameModal(EmojiImportIds.CreateSevenTvModalId(emoji.Id, emoji.IsAnimated), emoji.Name);

    private static Modal CreateNameModal(string customId, string emojiName) =>
        new ModalBuilder()
            .WithTitle("Import emoji")
            .WithCustomId(customId)
            .AddTextInput(
                label: "Emoji name",
                customId: EmojiImportIds.EmojiNameInputId,
                style: TextInputStyle.Short,
                placeholder: "letters, numbers, and underscores only",
                minLength: 2,
                maxLength: 32,
                required: true,
                value: emojiName)
            .Build();

    public static MessageComponent CreateSelectionComponents(IReadOnlyList<EmojiImportCandidate> emojis)
    {
        var options = emojis
            .Take(MaxSelectableEmojis)
            .Select(CreateSelectOption)
            .ToList();

        return new ComponentBuilder()
            .WithSelectMenu(
                customId: EmojiImportIds.EmojiSelectId,
                options: options,
                placeholder: "Choose an emoji to import",
                minValues: 1,
                maxValues: 1)
            .Build();
    }

    private static SelectMenuOptionBuilder CreateSelectOption(EmojiImportCandidate emoji)
    {
        var option = new SelectMenuOptionBuilder()
            .WithLabel($":{emoji.Name}:")
            .WithValue(EmojiImportIds.CreateSelectValue(emoji))
            .WithDescription(emoji.IsAnimated ? "Animated custom emoji" : "Custom emoji");

        return option.WithEmote(new Emote(emoji.Id, emoji.Name, emoji.IsAnimated));
    }
}
