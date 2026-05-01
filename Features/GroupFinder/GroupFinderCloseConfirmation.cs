namespace FlowBot;

public sealed record GroupFinderCloseConfirmation(
    GroupFinderButtonAction Action,
    ulong MessageId,
    ulong HostUserId);
