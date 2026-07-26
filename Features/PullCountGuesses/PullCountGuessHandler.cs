using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class PullCountGuessHandler(ILogger<PullCountGuessHandler> logger)
{
    public async Task HandleComponentAsync(SocketMessageComponent component)
    {
        if (PullCountGuessIds.TryParseCloseConfirmation(component.Data.CustomId, out var confirmation))
        {
            await HandleCloseConfirmationAsync(component, confirmation);
            return;
        }

        if (!PullCountGuessIds.TryParseButton(component.Data.CustomId, out var buttonState))
        {
            await component.RespondAsync("I could not identify this pull-count button.", ephemeral: true);
            return;
        }

        if (!PullCountGuessMessageBuilder.TryReadSession(
            component.Message,
            buttonState.IsClosed,
            out var session))
        {
            await component.RespondAsync("I could not read this guessing board.", ephemeral: true);
            return;
        }

        if (session.IsClosed)
        {
            await component.RespondAsync("Guessing is closed for this board.", ephemeral: true);
            return;
        }

        if (buttonState.Action == PullCountGuessButtonAction.AddOrUpdate)
        {
            await ShowGuessModalAsync(component);
            return;
        }

        if (buttonState.Action == PullCountGuessButtonAction.Remove)
        {
            await RemoveGuessAsync(component, session);
            return;
        }

        await CloseBoardAsync(component);
    }

    public async Task HandleModalAsync(SocketModal modal)
    {
        if (!PullCountGuessIds.TryParseModal(modal.Data.CustomId, out var modalState))
        {
            await modal.RespondAsync("I could not identify this pull-count guess form.", ephemeral: true);
            return;
        }

        var guessValue = modal.Data.Components
            .FirstOrDefault(component => component.CustomId == PullCountGuessIds.PullCountInputId)
            ?.Value;

        if (!TryParsePullCount(guessValue, out var pullCount))
        {
            await modal.RespondAsync(
                $"Enter a whole number between {PullCountGuessSession.MinPullCount} and {PullCountGuessSession.MaxPullCount}.",
                ephemeral: true);
            return;
        }

        try
        {
            var message = await modal.Channel.GetMessageAsync(modalState.MessageId);

            if (message is not IUserMessage userMessage)
            {
                await modal.RespondAsync("That guessing board no longer exists.", ephemeral: true);
                return;
            }

            if (!PullCountGuessMessageBuilder.TryReadSession(userMessage, isClosed: false, out var session))
            {
                await modal.RespondAsync("I could not read that guessing board.", ephemeral: true);
                return;
            }

            if (session.IsClosed)
            {
                await modal.RespondAsync("Guessing is closed for this board.", ephemeral: true);
                return;
            }

            var guesses = session.Guesses
                .Where(guess => guess.UserId != modal.User.Id)
                .Append(new PullCountGuess(modal.User.Id, pullCount))
                .ToArray();
            var updatedSession = session with { Guesses = guesses };

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = PullCountGuessMessageBuilder.BuildEmbed(updatedSession);
                properties.Components = PullCountGuessMessageBuilder.BuildComponents(updatedSession);
            });

            await modal.RespondAsync($"Your guess is now {pullCount}.", ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to update pull-count guessing board {MessageId}.",
                modalState.MessageId);
            await modal.RespondAsync("I could not update this guessing board.", ephemeral: true);
        }
    }

    private static async Task ShowGuessModalAsync(SocketMessageComponent component)
    {
        var modal = new ModalBuilder()
            .WithTitle("Add pull-count guess")
            .WithCustomId(PullCountGuessIds.CreateModalId(component.Message.Id))
            .AddTextInput(
                label: "Pull count",
                customId: PullCountGuessIds.PullCountInputId,
                style: TextInputStyle.Short,
                placeholder: "245",
                minLength: 1,
                maxLength: 4,
                required: true)
            .Build();

        await component.RespondWithModalAsync(modal);
    }

    private static async Task RemoveGuessAsync(
        SocketMessageComponent component,
        PullCountGuessSession session)
    {
        if (!session.Guesses.Any(guess => guess.UserId == component.User.Id))
        {
            await component.RespondAsync("You do not have a guess on this board.", ephemeral: true);
            return;
        }

        var updatedSession = session with
        {
            Guesses = session.Guesses
                .Where(guess => guess.UserId != component.User.Id)
                .ToArray(),
        };

        await component.UpdateAsync(properties =>
        {
            properties.Embed = PullCountGuessMessageBuilder.BuildEmbed(updatedSession);
            properties.Components = PullCountGuessMessageBuilder.BuildComponents(updatedSession);
        });
        await component.FollowupAsync("Your guess was removed.", ephemeral: true);
    }

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

        try
        {
            var message = await component.Channel.GetMessageAsync(confirmation.MessageId);

            if (message is not IUserMessage userMessage)
            {
                await UpdateEphemeralResponseAsync(component, "That guessing board no longer exists.");
                return;
            }

            if (!PullCountGuessMessageBuilder.TryReadSession(userMessage, isClosed: false, out var session))
            {
                await UpdateEphemeralResponseAsync(component, "I could not read that guessing board.");
                return;
            }

            var closedSession = session with { IsClosed = true };

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = PullCountGuessMessageBuilder.BuildEmbed(closedSession);
                properties.Components = PullCountGuessMessageBuilder.BuildComponents(closedSession);
            });
            await UpdateEphemeralResponseAsync(component, "Guessing closed.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to close pull-count guessing board {MessageId}.",
                confirmation.MessageId);
            await UpdateEphemeralResponseAsync(component, "I could not close this guessing board.");
        }
    }

    private static bool TryParsePullCount(string? value, out int pullCount) =>
        int.TryParse(value, out pullCount)
        && pullCount is >= PullCountGuessSession.MinPullCount and <= PullCountGuessSession.MaxPullCount;

    private static bool CanCloseBoard(SocketUser user) =>
        user is SocketGuildUser guildUser && guildUser.GuildPermissions.Administrator;

    private static Task UpdateEphemeralResponseAsync(SocketMessageComponent component, string content) =>
        component.UpdateAsync(properties =>
        {
            properties.Content = content;
            properties.Components = new ComponentBuilder().Build();
        });
}
