namespace FlowBot;

public sealed record PullCountGuessCloseConfirmation(
    PullCountGuessButtonAction Action,
    ulong MessageId);
