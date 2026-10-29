using SecureVoting.Application.Results;
using SecureVoting.Domain;
using Xunit;

namespace SecureVoting.UnitTests;

public sealed class ResultIntegrityTests
{
    [Fact]
    public void HashDetectsChangesToAbstentionsSeatsAndWinnerFlags()
    {
        var result = Snapshot();
        var original = result.IntegrityHash;

        result.Lines.First().PositionAbstentions++;
        result.IntegrityHash = ResultIntegrity.Compute(result);
        Assert.NotEqual(original, result.IntegrityHash);

        result = Snapshot();
        result.Lines.First().PositionSeatCount = 2;
        result.IntegrityHash = ResultIntegrity.Compute(result);
        Assert.NotEqual(original, result.IntegrityHash);

        result = Snapshot();
        result.Lines.First().IsWinner = !result.Lines.First().IsWinner;
        result.IntegrityHash = ResultIntegrity.Compute(result);
        Assert.NotEqual(original, result.IntegrityHash);
    }

    [Fact]
    public void VerifyRejectsATamperedSnapshot()
    {
        var result = Snapshot();
        Assert.True(ResultIntegrity.Verify(result));
        result.Lines.First().VoteCount++;
        Assert.False(ResultIntegrity.Verify(result));
    }

    private static ElectionResult Snapshot()
    {
        var result = new ElectionResult
        {
            ElectionId = Guid.NewGuid(),
            Version = 1,
            TotalEligible = 10,
            TotalBallots = 4,
            TurnoutPercentage = 40m,
            IntegrityHash = string.Empty
        };
        result.Lines.Add(new ElectionResultLine
        {
            PositionId = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            VoteCount = 4,
            PositionSeatCount = 1,
            PositionAbstentions = 0,
            Percentage = 100m,
            Rank = 1,
            IsWinner = true,
            IsTie = false
        });
        result.IntegrityHash = ResultIntegrity.Compute(result);
        return result;
    }
}
