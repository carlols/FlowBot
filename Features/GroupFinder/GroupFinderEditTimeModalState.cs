namespace FlowBot;

public sealed record GroupFinderEditTimeModalState(
    ulong MessageId,
    int? Capacity,
    bool? CapacityNoticeSent,
    bool? SessionStarted);