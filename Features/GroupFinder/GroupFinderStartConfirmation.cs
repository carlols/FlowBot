namespace FlowBot;

public sealed record GroupFinderStartConfirmation(
    GroupFinderButtonAction Action,
    ulong MessageId,
    ulong HostUserId,
    int? Capacity,
    bool? CapacityNoticeSent,
    bool? SessionStarted);
