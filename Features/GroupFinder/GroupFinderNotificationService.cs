using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderNotificationService(
    DiscordSocketClient client,
    ILogger<GroupFinderNotificationService> logger)
{
    public async Task SendCapacityNoticeAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        var messageLink = GroupFinderMessageLinks.Create(component);
        var message = $"Your group for **{session.GameName}** is full.";

        if (messageLink is not null)
        {
            message += $"{Environment.NewLine}{Environment.NewLine}Open group message:{Environment.NewLine}{messageLink}";
        }

        try
        {
            IUser? host = client.GetUser(session.HostUserId);
            host ??= await client.Rest.GetUserAsync(session.HostUserId);

            if (host is null)
            {
                logger.LogWarning(
                    "Could not DM group finder host {HostUserId} because the user was not cached.",
                    session.HostUserId);
                return;
            }

            await host.SendMessageAsync(message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to DM group finder host {HostUserId} that message {MessageId} is full.",
                session.HostUserId,
                component.Message.Id);
        }
    }

    public async Task SendSessionStartedAsync(IMessageChannel channel, GroupFinderSession session)
    {
        var playerMentions = string.Join(" ", session.PlayerIds.Select(playerId => $"<@{playerId}>"));

        await channel.SendMessageAsync(
            text: $"**{session.GameName}** is starting. Time to group up!{Environment.NewLine}{playerMentions}",
            allowedMentions: new AllowedMentions { UserIds = session.PlayerIds.ToList() });
    }
}
