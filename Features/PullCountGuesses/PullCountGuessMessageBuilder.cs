using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class PullCountGuessMessageBuilder
{
    public const string Description = "Guess how many pulls it will take before the first kill.";
    public const string EmptyGuesses = "No guesses yet.";

    private const string TitleSuffix = " - Pull Count Guesses";
    private const string StatusFieldName = "Status";
    private const string ClosedStatus = "Guessing closed";
    private const string OpenStatus = "Guessing open";
    private const int GuessesPerField = 10;

    public static Embed BuildEmbed(PullCountGuessSession session)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"{session.BossName}{TitleSuffix}")
            .WithDescription(Description)
            .AddField(StatusFieldName, session.IsClosed ? ClosedStatus : OpenStatus, inline: true)
            .AddField("Total guesses", session.Guesses.Count.ToString(), inline: true)
            .WithColor(session.IsClosed ? new Color(116, 127, 141) : new Color(255, 184, 77))
            .WithFooter(session.IsClosed
                ? "This board is closed."
                : "Use the buttons below to add, update, or remove your guess.");

        var orderedGuesses = OrderGuesses(session.Guesses).ToArray();

        if (orderedGuesses.Length == 0)
        {
            embed.AddField("Guesses", EmptyGuesses);
            return embed.Build();
        }

        for (var offset = 0; offset < orderedGuesses.Length; offset += GuessesPerField)
        {
            var chunk = orderedGuesses
                .Skip(offset)
                .Take(GuessesPerField)
                .ToArray();
            var firstPosition = offset + 1;
            var lastPosition = offset + chunk.Length;
            var value = string.Join(
                Environment.NewLine,
                chunk.Select((guess, index) =>
                    $"{firstPosition + index}. <@{guess.UserId}> - {guess.PullCount}"));

            embed.AddField($"Guesses {firstPosition}-{lastPosition}", value);
        }
        return embed.Build();
    }

    public static MessageComponent BuildComponents(PullCountGuessSession session) =>
        BuildComponents(session.IsClosed);

    private static MessageComponent BuildComponents(bool isClosed)
    {
        return new ComponentBuilder()
            .WithButton(
                label: "Add / Update Guess",
                customId: PullCountGuessIds.CreateAddOrUpdateId(isClosed),
                style: ButtonStyle.Success,
                disabled: isClosed)
            .WithButton(
                label: "Remove Guess",
                customId: PullCountGuessIds.CreateRemoveId(isClosed),
                style: ButtonStyle.Secondary,
                disabled: isClosed)
            .WithButton(
                label: "End Guessing",
                customId: PullCountGuessIds.CreateCloseId(isClosed),
                style: ButtonStyle.Danger,
                disabled: isClosed)
            .Build();
    }

    public static bool TryReadSession(
        IMessage message,
        bool isClosed,
        out PullCountGuessSession session)
    {
        session = new PullCountGuessSession("Unknown boss", isClosed, []);

        var embed = message.Embeds.FirstOrDefault();

        if (embed is null || string.IsNullOrWhiteSpace(embed.Title))
        {
            return false;
        }

        var bossName = embed.Title.EndsWith(TitleSuffix, StringComparison.Ordinal)
            ? embed.Title[..^TitleSuffix.Length]
            : embed.Title;
        var statusField = embed.Fields.FirstOrDefault(field => field.Name == StatusFieldName);
        var resolvedClosedState = isClosed || statusField.Value == ClosedStatus;
        var guesses = embed.Fields
            .Where(field => field.Name.StartsWith("Guesses", StringComparison.Ordinal))
            .SelectMany(field => GuessLineRegex().Matches(field.Value ?? string.Empty))
            .Select(match => new PullCountGuess(
                ulong.Parse(match.Groups["userId"].Value),
                int.Parse(match.Groups["pullCount"].Value)))
            .DistinctBy(guess => guess.UserId)
            .ToArray();

        session = new PullCountGuessSession(bossName, resolvedClosedState, guesses);
        return true;
    }

    private static IEnumerable<PullCountGuess> OrderGuesses(IEnumerable<PullCountGuess> guesses) =>
        guesses
            .OrderByDescending(guess => guess.PullCount)
            .ThenBy(guess => guess.UserId);

    [GeneratedRegex(@"^\d+\.\s+<@!?(?<userId>\d+)>\s+-\s+(?<pullCount>\d+)$", RegexOptions.Multiline)]
    private static partial Regex GuessLineRegex();
}
