namespace FlowBot;

public sealed record GroupFinderButtonState(
    GroupFinderButtonAction Action,
    int? Capacity,
    bool? CapacityNoticeSent,
    bool? SessionStarted);
