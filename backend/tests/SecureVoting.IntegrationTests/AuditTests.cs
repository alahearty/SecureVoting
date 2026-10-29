using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuditTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string StudentId = "IAUE/CS/22/001";
    private const string AuditorEmail = "auditor@demo.iaue.edu.ng";
    private const string OfficerEmail = "officer@demo.iaue.edu.ng";

    [Fact]
    public async Task AuditChainIsVerifiableAndDetectsDirectDatabaseTampering()
    {
        await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var auditor = await Fixture.LoginAsync(AuditorEmail, IntegrationApiFixture.DemoPassword);

        var before = await auditor.PostAsync("/api/audit/verify", null);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeResult = await ReadAsync<AuditVerificationDto>(before);
        Assert.True(beforeResult.IsValid,
            $"Audit chain invalid: {beforeResult.Message}; checked={beforeResult.EntriesChecked}; broken={beforeResult.BrokenAtSequence}; last={beforeResult.LastHash}");

        var firstAudit = await Fixture.QueryAsync(db => db.AuditLogs.OrderBy(a => a.Sequence).FirstAsync());
        firstAudit.DetailsJson = "{\"tampered\":true}";
        await Fixture.QueryAsync(async db =>
        {
            db.AuditLogs.Update(firstAudit);
            return await db.SaveChangesAsync();
        });

        var after = await auditor.PostAsync("/api/audit/verify", null);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterResult = await ReadAsync<AuditVerificationDto>(after);
        Assert.False(afterResult.IsValid);
        Assert.NotNull(afterResult.BrokenAtSequence);
    }

    [Fact]
    public async Task AuditChainDetectsDeletedRecord()
    {
        await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var auditor = await Fixture.LoginAsync(AuditorEmail, IntegrationApiFixture.DemoPassword);
        var before = await auditor.PostAsync("/api/audit/verify", null);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.True((await ReadAsync<AuditVerificationDto>(before)).IsValid);

        await Fixture.QueryAsync(async db =>
        {
            var entry = await db.AuditLogs.OrderBy(a => a.Sequence).FirstAsync();
            db.AuditLogs.Remove(entry);
            return await db.SaveChangesAsync();
        });

        var after = await auditor.PostAsync("/api/audit/verify", null);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var result = await ReadAsync<AuditVerificationDto>(after);
        Assert.False(result.IsValid);
        Assert.Contains("sequence", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditAndApiResponsesDoNotExposeVoterChoiceMapping()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var schedule = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/schedule",
            new ElectionStatusRequest { Reason = "Audit privacy test." });
        Assert.Equal(HttpStatusCode.NoContent, schedule.StatusCode);
        var open = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/open",
            new ElectionStatusRequest { Reason = "Audit privacy test." });
        Assert.Equal(HttpStatusCode.NoContent, open.StatusCode);

        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var ballot = await student.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{electionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(ballot);
        var choice = ballot.Positions[0].Candidates[0].Id;
        var submit = await student.PostAsJsonAsync(
            $"/api/student/elections/{electionId}/ballots",
            new CastBallotRequest
            {
                Positions = ballot.Positions.Select(position => new PositionChoiceRequest
                {
                    PositionId = position.Id,
                    CandidateIds = [position.Candidates[0].Id]
                }).ToList()
            });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);

        var audit = await Fixture.QueryAsync(async db =>
        {
            var entries = await db.AuditLogs
                .Where(a => a.EventType == "BallotAccepted")
                .Select(a => new { a.DetailsJson, a.ActorDisplay })
                .ToListAsync();
            return entries.Select(e => e.DetailsJson + "|" + e.ActorDisplay).ToList();
        });
        Assert.NotEmpty(audit);
        Assert.DoesNotContain(choice.ToString(), string.Join("|", audit));
        Assert.DoesNotContain(StudentId, string.Join("|", audit));
    }
}
