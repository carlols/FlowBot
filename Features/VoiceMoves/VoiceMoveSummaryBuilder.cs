using System.Text;
using Discord.WebSocket;

namespace FlowBot;

public static class VoiceMoveSummaryBuilder
{
    private const int MaxDisplayedFailedUsers = 10;

    public static string Build(
        VoiceMoveResult result,
        string movedSummary,
        string noMoveSummary,
        Func<int, string>? formatAlreadyInDestination = null,
        IEnumerable<string>? additionalDetails = null,
        Func<int, string>? formatFailureHeading = null)
    {
        var summary = new StringBuilder(
            result.MovedUsers.Count > 0
                ? movedSummary
                : noMoveSummary);

        if (result.AlreadyInDestination.Count > 0 && formatAlreadyInDestination is not null)
        {
            summary.Append(' ');
            summary.Append(formatAlreadyInDestination(result.AlreadyInDestination.Count));
        }

        foreach (var detail in additionalDetails ?? [])
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                continue;
            }

            summary.Append(' ');
            summary.Append(detail);
        }

        AppendFailures(summary, result.FailedUsers, formatFailureHeading);
        return summary.ToString();
    }

    private static void AppendFailures(
        StringBuilder summary,
        IReadOnlyCollection<SocketGuildUser> failedUsers,
        Func<int, string>? formatFailureHeading)
    {
        if (failedUsers.Count == 0)
        {
            return;
        }

        var failedMentions = string.Join(", ", failedUsers
            .Take(MaxDisplayedFailedUsers)
            .Select(user => user.Mention));
        var remainingCount = failedUsers.Count - MaxDisplayedFailedUsers;
        var remainingText = remainingCount > 0
            ? $", and {remainingCount} more"
            : string.Empty;

        var failureHeading = formatFailureHeading?.Invoke(failedUsers.Count)
            ?? $"Failed to move {failedUsers.Count}:";

        summary.Append($" {failureHeading} {failedMentions}{remainingText}.");
    }
}
