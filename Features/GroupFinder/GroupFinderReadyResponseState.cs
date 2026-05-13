namespace FlowBot;

public sealed record GroupFinderReadyResponseState(
    GroupFinderButtonAction Action,
    ulong MessageId,
    int? Capacity,
    bool? CapacityNoticeSent,
    bool? SessionStarted);
