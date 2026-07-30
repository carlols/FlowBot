namespace FlowBot;

public sealed record RoleVoiceMoveButtonState(
    RoleVoiceMoveAction Action,
    ulong RoleId,
    ulong DestinationChannelId);
