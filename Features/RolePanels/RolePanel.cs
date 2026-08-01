namespace FlowBot;

public sealed record RolePanel(
    string Title,
    string? Description,
    IReadOnlyList<ulong> RoleIds);
