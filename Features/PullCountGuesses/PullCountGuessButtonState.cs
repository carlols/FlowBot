namespace FlowBot;

public sealed record PullCountGuessButtonState(
    PullCountGuessButtonAction Action,
    bool IsClosed);
