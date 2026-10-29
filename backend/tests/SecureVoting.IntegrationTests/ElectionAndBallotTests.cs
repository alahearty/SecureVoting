using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ElectionAndBallotTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OfficerEmail = "officer@demo.iaue.edu.ng";
    private const string AuditorEmail = "auditor@demo.iaue.edu.ng";
    private const string SuperAdminEmail = "superadmin@demo.iaue.edu.ng";
    private const string StudentId = "IAUE/CS/22/001";

    [Fact]
    public async Task OfficerCanScheduleOpenAndCloseElectionThroughLifecycle()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);

        var schedule = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/schedule",
            new ElectionStatusRequest { Reason = "Register finalised." });
        var scheduleBody = await schedule.Content.ReadAsStringAsync();
        Assert.True(schedule.StatusCode == HttpStatusCode.NoContent,
            $"Schedule failed with {(int)schedule.StatusCode}: {scheduleBody}");

        var open = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/open",
            new ElectionStatusRequest { Reason = "Voting opened." });
        Assert.Equal(HttpStatusCode.NoContent, open.StatusCode);

        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await student.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        Assert.False(ballot.HasVoted);
        Assert.Equal(3, ballot.Positions.Count);

        var close = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/close",
            new ElectionStatusRequest { Reason = "Scheduled voting period ended." });
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);
    }

    [Fact]
    public async Task GenericLifecycleReviewActionCannotBypassResultSnapshot()
    {
        var electionId = await OpenElectionAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var close = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/close",
            new ElectionStatusRequest { Reason = "Close for lifecycle audit." });
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var genericReview = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/review",
            new ElectionStatusRequest { Reason = "Attempt to bypass snapshot generation." });
        Assert.Equal(HttpStatusCode.BadRequest, genericReview.StatusCode);

        var status = await Fixture.QueryAsync(db => db.Elections
            .Where(e => e.Id == electionId)
            .Select(e => e.Status)
            .SingleAsync());
        Assert.Equal(ElectionStatus.Closed, status);

        var properReview = await officer.PostAsync($"/api/elections/{electionId}/results/review", null);
        Assert.Equal(HttpStatusCode.OK, properReview.StatusCode);
    }
    [Fact]
    public async Task StudentCanSubmitExactlyOneBallotAndReceivesNonRevealingConfirmation()
    {
        var electionId = await OpenElectionAsync();
        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await student.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        Assert.NotNull(ballot.Positions);
        var request = new CastBallotRequest
        {
            Positions = ballot.Positions.Select(position => new PositionChoiceRequest
            {
                PositionId = position.Id,
                CandidateIds = [position.Candidates[0].Id]
            }).ToList()
        };

        var first = await student.PostAsJsonAsync(
            $"/api/student/elections/{electionId}/ballots", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var accepted = await ReadAsync<BallotAcceptedDto>(first);
        Assert.False(string.IsNullOrWhiteSpace(accepted.ConfirmationReference));
        Assert.DoesNotContain(request.Positions[0].CandidateIds[0].ToString(), accepted.ConfirmationReference);

        var second = await student.PostAsJsonAsync(
            $"/api/student/elections/{electionId}/ballots", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var state = await Fixture.QueryAsync(async db =>
        {
            var ballots = await db.Ballots.CountAsync(b => b.ElectionId == electionId);
            var entitlement = await db.VoterEntitlements.SingleAsync(e =>
                e.ElectionId == electionId && e.Student.MatricNumberNormalized == StudentId);
            return (ballots, entitlement.ConsumedAtUtc, entitlement.ConfirmationCodeHash);
        });
        Assert.Equal(1, state.ballots);
        Assert.NotNull(state.ConsumedAtUtc);
        Assert.NotNull(state.ConfirmationCodeHash);
        Assert.Null(typeof(Ballot).GetProperty("StudentId"));
        Assert.Null(typeof(Ballot).GetProperty("UserId"));
        Assert.Null(typeof(VoterEntitlement).GetProperty("BallotId"));
    }

    [Fact]
    public async Task ConcurrentSubmissionsFromOneVoterAcceptExactlyOne()
    {
        var electionId = await OpenElectionAsync();
        var firstClient = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await firstClient.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        var request = new CastBallotRequest
        {
            Positions = ballot.Positions.Select(position => new PositionChoiceRequest
            {
                PositionId = position.Id,
                CandidateIds = [position.Candidates[0].Id]
            }).ToList()
        };

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => firstClient.PostAsJsonAsync($"/api/student/elections/{electionId}/ballots", request))
            .ToArray();
        var responses = await Task.WhenAll(tasks);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await Fixture.QueryAsync(db => db.Ballots.CountAsync(b => b.ElectionId == electionId)));
    }

    [Fact]
    public async Task DistinctEligibleVotersCanVoteConcurrentlyWithoutLostBallots()
    {
        var electionId = await OpenElectionAsync();
        var ballotClient = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await ballotClient.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        var positions = ballot.Positions
            .Select(position => new PositionChoiceRequest
            {
                PositionId = position.Id,
                CandidateIds = [position.Candidates[0].Id]
            })
            .ToList();

        var studentNumbers = Enumerable.Range(1, 10).Select(i => $"IAUE/CS/22/{i:000}").ToArray();
        var clients = new List<HttpClient>();
        foreach (var student in studentNumbers)
        {
            clients.Add(await Fixture.LoginAsync(student, IntegrationApiFixture.DemoPassword));
        }

        var responses = await Task.WhenAll(clients.Select(client =>
            client.PostAsJsonAsync($"/api/student/elections/{electionId}/ballots",
                new CastBallotRequest { Positions = positions })));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(10, await Fixture.QueryAsync(db => db.Ballots.CountAsync(b => b.ElectionId == electionId)));
        Assert.Equal(10, await Fixture.QueryAsync(db => db.VoterEntitlements.CountAsync(e =>
            e.ElectionId == electionId && e.ConsumedAtUtc != null)));
    }

    [Fact]
    public async Task ForgedCandidateIdIsRejectedAndNoBallotIsStored()
    {
        var electionId = await OpenElectionAsync();
        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await student.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        var request = new CastBallotRequest
        {
            Positions = ballot.Positions.Select(position => new PositionChoiceRequest
            {
                PositionId = position.Id,
                CandidateIds = position == ballot.Positions[0]
                    ? [Guid.NewGuid()]
                    : [position.Candidates[0].Id]
            }).ToList()
        };

        var response = await student.PostAsJsonAsync($"/api/student/elections/{electionId}/ballots", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Fixture.QueryAsync(db => db.Ballots.CountAsync(b => b.ElectionId == electionId)));
        var consumed = await Fixture.QueryAsync(db => db.VoterEntitlements
            .Where(e => e.ElectionId == electionId && e.Student.MatricNumberNormalized == StudentId)
            .Select(e => e.ConsumedAtUtc)
            .SingleAsync());
        Assert.Null(consumed);
    }

    [Fact]
    public async Task BallotsCannotBeSubmittedAfterElectionClosure()
    {
        var electionId = await OpenElectionAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await student.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);

        var close = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/close",
            new ElectionStatusRequest { Reason = "Testing closure boundary." });
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var request = new CastBallotRequest
        {
            Positions = ballot.Positions.Select(position => new PositionChoiceRequest
            {
                PositionId = position.Id,
                CandidateIds = [position.Candidates[0].Id]
            }).ToList()
        };
        var response = await student.PostAsJsonAsync($"/api/student/elections/{electionId}/ballots", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ResultsRequireObserverVerificationBeforeSuperAdminPublication()
    {
        var electionId = await OpenElectionAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        await CastFirstChoiceBallotAsync(student, electionId);

        var close = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/close",
            new ElectionStatusRequest { Reason = "Voting completed for result test." });
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var review = await officer.PostAsync($"/api/elections/{electionId}/results/review", null);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var provisional = await ReadAsync<ElectionResultDto>(review);
        Assert.Equal(1, provisional.TotalBallots);
        Assert.Equal(ResultStatus.Provisional, provisional.Status);

        var auditor = await Fixture.LoginAsync(AuditorEmail, IntegrationApiFixture.DemoPassword);
        var verify = await auditor.PostAsJsonAsync(
            $"/api/elections/{electionId}/results/{provisional.Id}/verify",
            new VerifyResultRequest { Notes = "Aggregate reconciliation checked." });
        var verifyBody = await verify.Content.ReadAsStringAsync();
        Assert.True(verify.StatusCode == HttpStatusCode.OK,
            $"Verify failed with {(int)verify.StatusCode}: {verifyBody}");

        var superAdmin = await Fixture.LoginAsync(SuperAdminEmail, IntegrationApiFixture.DemoPassword);
        var publish = await superAdmin.PostAsJsonAsync(
            $"/api/elections/{electionId}/results/{provisional.Id}/publish",
            new ResultDecisionRequest { Notes = "Publication approved." });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = await ReadAsync<ElectionResultDto>(publish);
        Assert.Equal(ResultStatus.Published, published.Status);

        var publicResult = await student.GetFromJsonAsync<ElectionResultDto>(
            $"/api/elections/{electionId}/results/published", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(publicResult);
        Assert.Equal(1, publicResult.TotalBallots);
        Assert.Equal(3, publicResult.Positions.Count);
        Assert.All(publicResult.Positions, position => Assert.Single(position.Candidates, c => c.IsWinner));
    }

    private async Task<Guid> OpenElectionAsync()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var schedule = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/schedule",
            new ElectionStatusRequest { Reason = "Register finalised for test." });
        Assert.Equal(HttpStatusCode.NoContent, schedule.StatusCode);
        var open = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/open",
            new ElectionStatusRequest { Reason = "Voting opened for test." });
        Assert.Equal(HttpStatusCode.NoContent, open.StatusCode);
        return electionId;
    }

    private async Task CastFirstChoiceBallotAsync(HttpClient student, Guid electionId)
    {
        var ballot = await student.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        var response = await student.PostAsJsonAsync(
            $"/api/student/elections/{electionId}/ballots",
            new CastBallotRequest
            {
                Positions = ballot.Positions.Select(position => new PositionChoiceRequest
                {
                    PositionId = position.Id,
                    CandidateIds = [position.Candidates[0].Id]
                }).ToList()
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
