using Discord.WebSocket;

namespace FlowBot;

public sealed record RoleSelectionUpdateResult(
    IReadOnlyList<SocketRole> AddedRoles,
    IReadOnlyList<SocketRole> RemovedRoles,
    IReadOnlyList<SocketRole> FailedRoles,
    IReadOnlySet<ulong> EffectiveRoleIds);
