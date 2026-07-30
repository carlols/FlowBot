using Discord.WebSocket;

namespace FlowBot;

public sealed record VoiceMoveResult(
    IReadOnlyCollection<SocketGuildUser> MovedUsers,
    IReadOnlyCollection<SocketGuildUser> AlreadyInDestination,
    IReadOnlyCollection<SocketGuildUser> FailedUsers);
