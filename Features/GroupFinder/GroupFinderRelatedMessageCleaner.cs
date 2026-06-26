using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderRelatedMessageCleaner(
    DiscordSocketClient client,
    ILogger<GroupFinderRelatedMessageCleaner> logger)
{
    private const int MessagesToScan = 100;

    public async Task<int> DeleteRelatedMessagesAsync(IMessage parentMessage)
    {
        var currentUserId = client.CurrentUser?.Id;
        if (currentUserId is null)
        {
            logger.LogWarning("Could not clean up related group finder messages because Flowbot's current user was not available.");
            return 0;
        }

        IReadOnlyCollection<IMessage> candidates;

        try
        {
            candidates = await GetCandidateMessagesAsync(parentMessage);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to scan for related group finder messages for parent message {ParentMessageId}.",
                parentMessage.Id);
            return 0;
        }
        var relatedMessages = candidates
            .Where(message => message.Id != parentMessage.Id)
            .Where(message => message.Author.Id == currentUserId)
            .Where(message => IsRelatedToParent(message, parentMessage.Id))
            .DistinctBy(message => message.Id)
            .ToArray();

        var deletedCount = 0;

        foreach (var message in relatedMessages)
        {
            try
            {
                await message.DeleteAsync();
                deletedCount++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to delete related group finder message {MessageId} for parent message {ParentMessageId}.",
                    message.Id,
                    parentMessage.Id);
            }
        }

        return deletedCount;
    }

    private static async Task<IReadOnlyCollection<IMessage>> GetCandidateMessagesAsync(IMessage parentMessage)
    {
        var recentMessages = await parentMessage.Channel
            .GetMessagesAsync(MessagesToScan)
            .FlattenAsync();

        var messagesAfterParent = await parentMessage.Channel
            .GetMessagesAsync(parentMessage.Id, Direction.After, MessagesToScan)
            .FlattenAsync();

        return recentMessages
            .Concat(messagesAfterParent)
            .DistinctBy(message => message.Id)
            .ToArray();
    }

    private static bool IsRelatedToParent(IMessage message, ulong parentMessageId) =>
        message.Reference?.MessageId.IsSpecified == true && message.Reference.MessageId.Value == parentMessageId
        || HasReadyCheckButtonForParent(message, parentMessageId);

    private static bool HasReadyCheckButtonForParent(IMessage message, ulong parentMessageId) =>
        message.Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .Any(button =>
                GroupFinderButtonIds.TryParseReadyResponse(button.CustomId ?? string.Empty, out var readyResponse)
                && readyResponse.MessageId == parentMessageId);
}
