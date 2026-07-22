using System.Security.Cryptography;

namespace FlowBot;

public sealed class GroupFinderTeamScrambler
{
    public IReadOnlyList<IReadOnlyList<ulong>> CreateTeams(IReadOnlyList<ulong> playerIds)
    {
        var shuffledPlayerIds = playerIds.ToArray();

        for (var index = shuffledPlayerIds.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (shuffledPlayerIds[index], shuffledPlayerIds[swapIndex]) = (shuffledPlayerIds[swapIndex], shuffledPlayerIds[index]);
        }

        var firstTeamSize = (shuffledPlayerIds.Length + 1) / 2;

        return
        [
            shuffledPlayerIds.Take(firstTeamSize).ToArray(),
            shuffledPlayerIds.Skip(firstTeamSize).ToArray(),
        ];
    }
}
