using Discord.WebSocket;

namespace FlowBot;

public sealed partial class PullCountGuessHandler
{
    private readonly DiscordMessageMutationLock _messageMutationLock;
    private readonly ILogger<PullCountGuessHandler> _logger;

    public PullCountGuessHandler(
        DiscordMessageMutationLock messageMutationLock,
        ILogger<PullCountGuessHandler> logger)
    {
        _messageMutationLock = messageMutationLock;
        _logger = logger;
    }

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
            case PullCountGuessButtonAction.Close:
                await CloseBoardAsync(component);
                break;
            default:
                await component.RespondAsync("I could not identify this pull-count action.", ephemeral: true);
                break;
        }
    }

    public Task HandleModalAsync(SocketModal modal) =>
        HandleGuessModalAsync(modal);
}
