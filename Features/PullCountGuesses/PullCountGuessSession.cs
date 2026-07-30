namespace FlowBot;

public sealed record PullCountGuessSession(
    string BossName,
    bool IsClosed,
    IReadOnlyList<PullCountGuess> Guesses)
{
    public const int MinPullCount = 1;
    public const int MaxBossNameLength = 200;
    public const int MaxPullCount = 9999;

    public static PullCountGuessSession Create(string bossName) =>
        new(bossName.Trim(), false, []);
}
