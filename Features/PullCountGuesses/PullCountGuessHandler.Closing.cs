using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class PullCountGuessHandler
{
    private static async Task CloseBoardAsync(SocketMessageComponent component)
    {
        if (!CanCloseBoard(component.User))
        {
            await component.RespondAsync("Only server admins can end guessing.", ephemeral: true);
            return;
        }

        var components = new ComponentBuilder()
            .WithButton(
                label: "Confirm end",
                customId: PullCountGuessIds.CreateConfirmCloseId(component.Message.Id),
                style: ButtonStyle.Danger)
            .WithButton(
                label: "Cancel",
                customId: PullCountGuessIds.CreateCancelCloseId(),
                style: ButtonStyle.Secondary)
            .Build();

        await component.RespondAsync(
            "Ending guessing will close this board and disable its buttons.",
            components: components,
            ephemeral: true);
    }

    private async Task HandleCloseConfirmationAsync(
        SocketMessageComponent component,
        PullCountGuessCloseConfirmation confirmation)
    {
        if (confirmation.Action == PullCountGuessButtonAction.CancelClose)
        {
            await UpdateEphemeralResponseAsync(component, "End guessing cancelled.");
            return;
        }

        if (!CanCloseBoard(component.User))
        {
            await UpdateEphemeralResponseAsync(component, "Only server admins can end guessing.");
            return;
        }

        await UpdateEphemeralResponseAsync(component, "Closing guessing...");

        try
        {
            using (await _messageMutationLock.AcquireAsync(confirmation.MessageId))
            {
                var current = await LoadCurrentBoardAsync(component.Channel, confirmation.MessageId);
                if (current is null)
                {
                    await component.FollowupAsync("That guessing board no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (session.IsClosed)
                {
                    await component.FollowupAsync("Guessing is already closed for this board.", ephemeral: true);
                    return;
                }

                await UpdateBoardAsync(userMessage, session with { IsClosed = true });
            }

            await component.FollowupAsync("Guessing closed.", ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to close pull-count guessing board {MessageId}.",
                confirmation.MessageId);
            await component.FollowupAsync("I could not close this guessing board.", ephemeral: true);
        }
    }

    private static bool CanCloseBoard(SocketUser user) =>
        user is SocketGuildUser guildUser && guildUser.GuildPermissions.Administrator;
}
