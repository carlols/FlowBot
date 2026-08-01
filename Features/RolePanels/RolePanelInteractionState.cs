namespace FlowBot;

public sealed record RolePanelInteractionState(
    RolePanelAction Action,
    ulong ChannelId = 0,
    ulong MessageId = 0);
