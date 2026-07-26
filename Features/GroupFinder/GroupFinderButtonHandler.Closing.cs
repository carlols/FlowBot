using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class GroupFinderButtonHandler
{
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
            "You can close this group. Confirming will delete the group finder message and related Flowbot ready/start messages.",
            components: components,
            ephemeral: true);
    }

    private async Task HandleCloseConfirmationAsync(
        SocketMessageComponent component,
        GroupFinderCloseConfirmation confirmation)
    {
        if (confirmation.Action == GroupFinderButtonAction.CancelClose)
        {
            await UpdateEphemeralResponseAsync(component, "Close cancelled.");
            return;
        }

        if (!CanCloseGroup(component.User, confirmation.HostUserId))
        {
            await UpdateEphemeralResponseAsync(component, "Only the host or moderators can close this group.");
            return;
        }

        try
        {
            var message = await component.Channel.GetMessageAsync(confirmation.MessageId);

            if (message is null)
            {
                await UpdateEphemeralResponseAsync(component, "That group message no longer exists.");
                return;
            }

            var deletedRelatedMessages = await _relatedMessageCleaner.DeleteRelatedMessagesAsync(message);
            await message.DeleteAsync();

            await UpdateEphemeralResponseAsync(
                component,
                deletedRelatedMessages > 0
                    ? $"Group closed. Deleted {deletedRelatedMessages} related Flowbot message{(deletedRelatedMessages == 1 ? string.Empty : "s")}."
                    : "Group closed.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete group finder message {MessageId}.",
                confirmation.MessageId);
            await UpdateEphemeralResponseAsync(component, "I could not delete this group message.");
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

}