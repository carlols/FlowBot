using Discord;

namespace FlowBot;

public sealed record GroupFinderSession(
    string GameName,
    string? Description,
    int Capacity,
    ulong HostUserId,
    IReadOnlyList<ulong> PlayerIds)
{
    public const int MinCapacity = 1;
    public const int MaxCapacity = 20;

    public bool IsFull => PlayerIds.Count >= Capacity;

    public static GroupFinderSession Create(
        string gameName,
        string? description,
        int capacity,
        IUser creator)
    {
        return new GroupFinderSession(
            gameName.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            capacity,
            creator.Id,
            [creator.Id]);
    }
}
