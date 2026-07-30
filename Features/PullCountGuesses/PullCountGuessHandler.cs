using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class PullCountGuessHandler(
    DiscordMessageMutationLock messageMutationLock,
    ILogger<PullCountGuessHandler> logger)
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

        switch (buttonState.Action)
        {
            case PullCountGuessButtonAction.AddOrUpdate:
                await ShowGuessModalAsync(component);
                break;
            case PullCountGuessButtonAction.Remove:
                await RemoveGuessAsync(component);
                break;
            default:
                await CloseBoardAsync(component);
                break;
        }
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

        await modal.DeferAsync(ephemeral: true);

        try
        {
            using (await messageMutationLock.AcquireAsync(modalState.MessageId))
            {
                var current = await LoadCurrentBoardAsync(modal.Channel, modalState.MessageId);
                if (current is null)
                {
                    await modal.FollowupAsync("That guessing board no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (session.IsClosed)
                {
                    await modal.FollowupAsync("Guessing is closed for this board.", ephemeral: true);
                    return;
                }

                var guesses = session.Guesses
                    .Where(guess => guess.UserId != modal.User.Id)
                    .Append(new PullCountGuess(modal.User.Id, pullCount))
                    .ToArray();
                var updatedSession = session with { Guesses = guesses };

                await UpdateBoardAsync(userMessage, updatedSession);
            }

            await modal.FollowupAsync($"Your guess is now {pullCount}.", ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to update pull-count guessing board {MessageId}.",
                modalState.MessageId);
            await modal.FollowupAsync("I could not update this guessing board.", ephemeral: true);
        }
    }

    private static Task ShowGuessModalAsync(SocketMessageComponent component)
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

        return component.RespondWithModalAsync(modal);
    }

    private async Task RemoveGuessAsync(SocketMessageComponent component)
    {
        await component.DeferAsync(ephemeral: true);

        try
        {
            using (await messageMutationLock.AcquireAsync(component.Message.Id))
            {
                var current = await LoadCurrentBoardAsync(component.Channel, component.Message.Id);
                if (current is null)
                {
                    await component.FollowupAsync("That guessing board no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (session.IsClosed)
                {
                    await component.FollowupAsync("Guessing is closed for this board.", ephemeral: true);
                    return;
                }

                if (!session.Guesses.Any(guess => guess.UserId == component.User.Id))
                {
                    await component.FollowupAsync("You do not have a guess on this board.", ephemeral: true);
                    return;
                }

                var updatedSession = session with
                {
                    Guesses = session.Guesses
                        .Where(guess => guess.UserId != component.User.Id)
                        .ToArray(),
                };

                await UpdateBoardAsync(userMessage, updatedSession);
            }

            await component.FollowupAsync("Your guess was removed.", ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to remove a guess from board {MessageId}.",
                component.Message.Id);
            await component.FollowupAsync("I could not update this guessing board.", ephemeral: true);
        }
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

        await UpdateEphemeralResponseAsync(component, "Closing guessing...");

        try
        {
            using (await messageMutationLock.AcquireAsync(confirmation.MessageId))
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
            logger.LogWarning(
                exception,
                "Failed to close pull-count guessing board {MessageId}.",
                confirmation.MessageId);
            await component.FollowupAsync("I could not close this guessing board.", ephemeral: true);
        }
    }

    private static async Task<(IUserMessage Message, PullCountGuessSession Session)?> LoadCurrentBoardAsync(
        IMessageChannel channel,
        ulong messageId)
    {
        var message = await channel.GetMessageAsync(messageId, CacheMode.AllowDownload);

        return message is IUserMessage userMessage
            && PullCountGuessMessageBuilder.TryReadSession(userMessage, out var session)
                ? (userMessage, session)
                : null;
    }

    private static Task UpdateBoardAsync(IUserMessage message, PullCountGuessSession session) =>
        message.ModifyAsync(properties =>
        {
            properties.Embed = PullCountGuessMessageBuilder.BuildEmbed(session);
            properties.Components = PullCountGuessMessageBuilder.BuildComponents(session);
        });

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