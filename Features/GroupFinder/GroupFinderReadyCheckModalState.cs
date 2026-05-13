namespace FlowBot;

public sealed record GroupFinderReadyCheckModalState(
    ulong MessageId,
    int? Capacity,
    bool? CapacityNoticeSent,
    bool? SessionStarted);
