using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class PullCountGuessHandler
{
    private async Task HandleGuessModalAsync(SocketModal modal)
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
            using (await _messageMutationLock.AcquireAsync(modalState.MessageId))
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

                await UpdateBoardAsync(userMessage, session with { Guesses = guesses });
            }

            await modal.FollowupAsync($"Your guess is now {pullCount}.", ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
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
            using (await _messageMutationLock.AcquireAsync(component.Message.Id))
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
            _logger.LogWarning(
                exception,
                "Failed to remove a guess from board {MessageId}.",
                component.Message.Id);
            await component.FollowupAsync("I could not update this guessing board.", ephemeral: true);
        }
    }

    private static bool TryParsePullCount(string? value, out int pullCount) =>
        int.TryParse(value, out pullCount)
        && pullCount is >= PullCountGuessSession.MinPullCount and <= PullCountGuessSession.MaxPullCount;
}
