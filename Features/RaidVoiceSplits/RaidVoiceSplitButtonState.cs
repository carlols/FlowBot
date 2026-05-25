namespace FlowBot;

public sealed record RaidVoiceSplitButtonState(
    RaidVoiceSplitAction Action,
    ulong RoleId,
    ulong TargetChannelId);
