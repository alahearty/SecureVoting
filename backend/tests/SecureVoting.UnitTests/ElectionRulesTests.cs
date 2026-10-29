using SecureVoting.Application.Elections;
using SecureVoting.Application.Security;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using Xunit;

namespace SecureVoting.UnitTests;

public sealed class ElectionRulesTests
{
    [Fact]
    public void LifecycleAllowsOnlyConfiguredTransitions()
    {
        Assert.True(ElectionStateMachine.CanTransition(ElectionStatus.Draft, ElectionStatus.Scheduled));
        Assert.True(ElectionStateMachine.CanTransition(ElectionStatus.Scheduled, ElectionStatus.Open));
        Assert.True(ElectionStateMachine.CanTransition(ElectionStatus.Open, ElectionStatus.Closed));
        Assert.True(ElectionStateMachine.CanTransition(ElectionStatus.Closed, ElectionStatus.UnderReview));
        Assert.True(ElectionStateMachine.CanTransition(ElectionStatus.UnderReview, ElectionStatus.Published));
        Assert.True(ElectionStateMachine.CanTransition(ElectionStatus.Published, ElectionStatus.UnderReview));
        Assert.False(ElectionStateMachine.CanTransition(ElectionStatus.Draft, ElectionStatus.Open));
        Assert.False(ElectionStateMachine.CanTransition(ElectionStatus.Cancelled, ElectionStatus.Scheduled));
        Assert.False(ElectionStateMachine.CanTransition(ElectionStatus.Published, ElectionStatus.Cancelled));
    }

    [Fact]
    public void BallotsAreOnlyAcceptedInOpenState()
    {
        Assert.True(ElectionStateMachine.AcceptsBallots(ElectionStatus.Open));
        Assert.False(ElectionStateMachine.AcceptsBallots(ElectionStatus.Scheduled));
        Assert.False(ElectionStateMachine.AcceptsBallots(ElectionStatus.Suspended));
        Assert.False(ElectionStateMachine.AcceptsBallots(ElectionStatus.Closed));
    }

    [Fact]
    public void BallotValidationRejectsIneligibleAndClosedElection()
    {
        var (election, positions, candidates) = BuildBallot();
        var choices = SingleChoice(positions[0]);
        Assert.Throws<ForbiddenOperationException>(() =>
            BallotValidator.Validate(election, positions, candidates, choices, false, NowInside(election)));
        election.Status = ElectionStatus.Closed;
        Assert.Throws<ConflictException>(() =>
            BallotValidator.Validate(election, positions, candidates, choices, true, NowInside(election)));
    }

    [Fact]
    public void BallotValidationRejectsForgedCandidate()
    {
        var (election, positions, candidates) = BuildBallot();
        var choices = SingleChoice(positions[0]);
        choices[0].CandidateIds = [Guid.NewGuid()];
        Assert.Throws<ValidationException>(() =>
            BallotValidator.Validate(election, positions, candidates, choices, true, NowInside(election)));
    }

    [Fact]
    public void BallotValidationRejectsMissingPositionAndTooManySelections()
    {
        var (election, positions, candidates) = BuildBallot();
        Assert.Throws<ValidationException>(() =>
            BallotValidator.Validate(election, positions, candidates, [], true, NowInside(election)));

        var choices = SingleChoice(positions[0]);
        var second = candidates.First(c => c.PositionId == positions[0].Id);
        var another = candidates.Where(c => c.PositionId == positions[0].Id).Skip(1).First();
        // Only one approved candidate exists for the first test position; add a real second candidate.
        var extra = new Candidate
        {
            ElectionId = election.Id,
            PositionId = positions[0].Id,
            StudentId = Guid.NewGuid(),
            DisplayName = "Extra",
            Manifesto = "Extra candidate",
            Status = CandidateStatus.Approved
        };
        positions[0].MaxSelections = 1;
        choices[0].CandidateIds = [second.Id, extra.Id];
        Assert.Throws<ValidationException>(() =>
            BallotValidator.Validate(election, positions, candidates.Append(extra), choices, true, NowInside(election)));
    }

    [Fact]
    public void AbstentionIsAllowedOnlyWhenConfigured()
    {
        var (election, positions, candidates) = BuildBallot();
        election.Rule.AllowAbstention = true;
        var choices = positions.Select(p => new PositionChoiceRequest { PositionId = p.Id }).ToList();
        var result = BallotValidator.Validate(election, positions, candidates, choices, true, NowInside(election));
        Assert.All(result.Positions, p => Assert.True(p.IsAbstention));

        election.Rule.AllowAbstention = false;
        Assert.Throws<ValidationException>(() =>
            BallotValidator.Validate(election, positions, candidates, choices, true, NowInside(election)));
    }

    [Fact]
    public void MatriculationNormalizationIsStrictAndStable()
    {
        Assert.True(MatriculationNumber.TryNormalize(" iaue/cs/22/001 ", out var normalized));
        Assert.Equal("IAUE/CS/22/001", normalized);
        Assert.False(MatriculationNumber.TryNormalize("", out _));
        Assert.False(MatriculationNumber.TryNormalize("ABC", out _));
        Assert.False(MatriculationNumber.TryNormalize("IAUE/CS/22/001<script>", out _));
        Assert.False(MatriculationNumber.TryNormalize("IAUE/CS/AA/BBB", out _));
    }

    [Fact]
    public void TokensAreRandomAndHashable()
    {
        var first = TokenGenerator.Create();
        var second = TokenGenerator.Create();
        Assert.NotEqual(first, second);
        Assert.Equal(64, TokenGenerator.Sha256Hex(first).Length);
        Assert.Equal(TokenGenerator.Sha256Hex(first), TokenGenerator.Sha256Hex(first));
    }

    private static (Election, List<ElectionPosition>, List<Candidate>) BuildBallot()
    {
        var election = new Election
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = ElectionStatus.Open,
            OpensAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ClosesAtUtc = DateTime.UtcNow.AddHours(1),
            Rule = new ElectionRule { AllowAbstention = true, TieHandling = TieHandling.RunoffRequired }
        };
        var position = new ElectionPosition
        {
            Id = Guid.NewGuid(),
            ElectionId = election.Id,
            Title = "President",
            SeatCount = 1,
            MaxSelections = 1,
            IsActive = true
        };
        var candidates = new List<Candidate>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ElectionId = election.Id,
                PositionId = position.Id,
                StudentId = Guid.NewGuid(),
                DisplayName = "One",
                Manifesto = "Manifesto one",
                Status = CandidateStatus.Approved
            },
            new()
            {
                Id = Guid.NewGuid(),
                ElectionId = election.Id,
                PositionId = position.Id,
                StudentId = Guid.NewGuid(),
                DisplayName = "Two",
                Manifesto = "Manifesto two",
                Status = CandidateStatus.Approved
            },
        };
        return (election, [position], candidates);
    }

    private static DateTime NowInside(Election election) => election.OpensAtUtc.AddMinutes(1);

    private static List<PositionChoiceRequest> SingleChoice(ElectionPosition position) =>
    [
        new PositionChoiceRequest
        {
            PositionId = position.Id,
            CandidateIds = [Guid.Empty]
        }
    ];
}
