using SecureVoting.Application.Results;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using Xunit;

namespace SecureVoting.UnitTests;

public sealed class ResultCalculatorTests
{
    [Fact]
    public void CountsVotesPercentagesAbstentionsAndTurnout()
    {
        var (election, positions, candidates) = Build();
        var ballots = new[]
        {
            Ballot(election.Id, positions[0].Id, candidates[0].Id, abstain: false),
            Ballot(election.Id, positions[0].Id, candidates[1].Id, abstain: false),
            Ballot(election.Id, positions[0].Id, null, abstain: true)
        };
        var result = ResultCalculator.Calculate(election, positions, candidates, ballots, 5);
        var position = Assert.Single(result.Positions);
        Assert.Equal(3, result.TotalBallots);
        Assert.Equal(60m, result.TurnoutPercentage);
        Assert.Equal(1, position.Abstentions);
        Assert.Equal(1, position.Candidates.Single(c => c.CandidateId == candidates[0].Id).Votes);
        Assert.Equal(1, position.Candidates.Single(c => c.CandidateId == candidates[1].Id).Votes);
        Assert.Equal(50m, position.Candidates.Single(c => c.CandidateId == candidates[0].Id).Percentage);
        Assert.True(position.Candidates.All(c => c.IsTie));
        Assert.All(position.Candidates, c => Assert.False(c.IsWinner));
    }

    [Fact]
    public void SharedOfficeRuleMarksAllBoundaryTiedCandidatesAsWinners()
    {
        var (election, positions, candidates) = Build();
        election.Rule.TieHandling = TieHandling.SharedOffice;
        positions[0].SeatCount = 1;
        var ballots = new[]
        {
            Ballot(election.Id, positions[0].Id, candidates[0].Id, false),
            Ballot(election.Id, positions[0].Id, candidates[1].Id, false)
        };
        var result = ResultCalculator.Calculate(election, positions, candidates, ballots, 2);
        var position = Assert.Single(result.Positions);
        Assert.All(position.Candidates, c => Assert.True(c.IsWinner));
        Assert.Contains("shared office", position.TieNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NontiedTopCandidateWinsAndZeroVoteCandidatesHaveZeroRank()
    {
        var (election, positions, candidates) = Build();
        var ballots = new[]
        {
            Ballot(election.Id, positions[0].Id, candidates[0].Id, false),
            Ballot(election.Id, positions[0].Id, candidates[0].Id, false),
            Ballot(election.Id, positions[0].Id, candidates[1].Id, false)
        };
        var result = ResultCalculator.Calculate(election, positions, candidates, ballots, 3);
        var winner = result.Positions[0].Candidates.Single(c => c.CandidateId == candidates[0].Id);
        var loser = result.Positions[0].Candidates.Single(c => c.CandidateId == candidates[1].Id);
        Assert.True(winner.IsWinner);
        Assert.False(loser.IsWinner);
        Assert.Equal(1, winner.Rank);
        Assert.Equal(2, loser.Rank);
    }

    [Fact]
    public void AuditChainHashIsDeterministicAndSensitiveToDetails()
    {
        var id = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var when = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var first = AuditHash.Compute(1, eventId, when, id, "Actor", "Event", "Election", "1",
            AuditOutcome.Success, "corr", "{\"b\":2,\"a\":1}", new string('0', 64));
        var second = AuditHash.Compute(1, eventId, when, id, "Actor", "Event", "Election", "1",
            AuditOutcome.Success, "corr", "{\"a\":1,\"b\":2}", new string('0', 64));
        var changed = AuditHash.Compute(1, eventId, when, id, "Actor", "Event", "Election", "1",
            AuditOutcome.Success, "corr", "{\"a\":1,\"b\":3}", new string('0', 64));
        Assert.Equal(first, second);
        Assert.NotEqual(first, changed);
        Assert.Equal(64, first.Length);
    }

    private static (Election, List<ElectionPosition>, List<Candidate>) Build()
    {
        var election = new Election
        {
            Id = Guid.NewGuid(),
            Status = ElectionStatus.Closed,
            Rule = new ElectionRule { TieHandling = TieHandling.RunoffRequired, AllowAbstention = true }
        };
        var position = new ElectionPosition
        {
            Id = Guid.NewGuid(),
            ElectionId = election.Id,
            Title = "President",
            DisplayOrder = 1,
            SeatCount = 1,
            MaxSelections = 1,
            IsActive = true
        };
        var candidates = new[]
        {
            new Candidate
            {
                Id = Guid.NewGuid(), ElectionId = election.Id, PositionId = position.Id,
                StudentId = Guid.NewGuid(), DisplayName = "Ada", Manifesto = "A",
                Status = CandidateStatus.Approved
            },
            new Candidate
            {
                Id = Guid.NewGuid(), ElectionId = election.Id, PositionId = position.Id,
                StudentId = Guid.NewGuid(), DisplayName = "Bola", Manifesto = "B",
                Status = CandidateStatus.Approved
            }
        };
        return (election, [position], candidates.ToList());
    }

    private static Ballot Ballot(Guid electionId, Guid positionId, Guid? candidateId, bool abstain)
    {
        var ballot = new Ballot { ElectionId = electionId, SubmittedAtUtc = DateTime.UtcNow };
        if (abstain || candidateId is null)
        {
            ballot.Abstentions.Add(new BallotAbstention
            {
                ElectionPositionId = positionId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            ballot.Selections.Add(new BallotSelection
            {
                ElectionPositionId = positionId,
                CandidateId = candidateId.Value,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return ballot;
    }
}
