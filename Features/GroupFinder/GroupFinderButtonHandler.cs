using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderButtonHandler(ILogger<GroupFinderButtonHandler> logger)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (GroupFinderButtonIds.TryParseCloseConfirmation(
            component.Data.CustomId,
            out var confirmationAction,
            out var messageId,
            out var hostUserId))
        {
            await HandleCloseConfirmationAsync(component, confirmationAction, messageId, hostUserId);
            return;
        }

        if (!GroupFinderButtonIds.TryParse(component.Data.CustomId, out var action, out var capacity))
        {
            await component.RespondAsync("I could not identify this group finder button.", ephemeral: true);
            return;
        }

        if (!GroupFinderMessageBuilder.TryReadSession(component.Message, capacity, out var session))
        {
            await component.RespondAsync("I could not read this group finder message.", ephemeral: true);
            return;
        }

        var playerIds = session.PlayerIds.ToList();
        var userId = component.User.Id;
        var isRegistered = playerIds.Contains(userId);

        if (action == GroupFinderButtonAction.Close)
        {
            await CloseGroupAsync(component, session);
            return;
        }

        if (action == GroupFinderButtonAction.Join)
        {
            if (isRegistered)
            {
                await component.RespondAsync("You are already in this group.", ephemeral: true);
                return;
            }

            if (playerIds.Count >= session.Capacity)
            {
                await component.RespondAsync("This group is already full.", ephemeral: true);
                return;
            }

            playerIds.Add(userId);
            await UpdateGroupMessageAsync(component, session with { PlayerIds = playerIds });
            return;
        }

        if (!isRegistered)
        {
            await component.RespondAsync("You are not in this group.", ephemeral: true);
            return;
        }

        playerIds.Remove(userId);
        await UpdateGroupMessageAsync(component, session with { PlayerIds = playerIds });
    }

    private async Task CloseGroupAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (!CanCloseGroup(component.User, session.HostUserId))
        {
            await component.RespondAsync("Only the host or moderators can close this group.", ephemeral: true);
            return;
        }

        var components = new ComponentBuilder()
            .WithButton(
                label: "Confirm close",
                customId: GroupFinderButtonIds.CreateConfirmCloseId(component.Message.Id, session.HostUserId),
                style: ButtonStyle.Danger)
            .WithButton(
                label: "Cancel",
                customId: GroupFinderButtonIds.CreateCancelCloseId(),
                style: ButtonStyle.Secondary)
            .Build();

        await component.RespondAsync(
            "You can close this group. Confirming will delete the group finder message.",
            components: components,
            ephemeral: true);
    }

    private async Task HandleCloseConfirmationAsync(
        SocketMessageComponent component,
        GroupFinderButtonAction action,
        ulong messageId,
        ulong hostUserId)
    {
        if (action == GroupFinderButtonAction.CancelClose)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Close cancelled.";
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        if (!CanCloseGroup(component.User, hostUserId))
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Only the host or moderators can close this group.";
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        try
        {
            var message = await component.Channel.GetMessageAsync(messageId);

            if (message is null)
            {
                await component.UpdateAsync(properties =>
                {
                    properties.Content = "That group message no longer exists.";
                    properties.Components = new ComponentBuilder().Build();
                });
                return;
            }

            await message.DeleteAsync();
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Group closed.";
                properties.Components = new ComponentBuilder().Build();
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to delete group finder message {MessageId}.", messageId);
            await component.UpdateAsync(properties =>
            {
                properties.Content = "I could not delete this group message.";
                properties.Components = new ComponentBuilder().Build();
            });
        }
    }

    private static bool CanCloseGroup(SocketUser user, ulong hostUserId)
    {
        if (user.Id == hostUserId)
        {
            return true;
        }

        return user is SocketGuildUser guildUser
            && (guildUser.GuildPermissions.Administrator
                || guildUser.GuildPermissions.ManageMessages);
    }

    private async Task UpdateGroupMessageAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        try
        {
            await component.UpdateAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(session);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(session.Capacity, session.PlayerIds.Count);
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to update group finder message {MessageId}.", component.Message.Id);
            await component.RespondAsync("I could not update this group message.", ephemeral: true);
        }
    }
}
