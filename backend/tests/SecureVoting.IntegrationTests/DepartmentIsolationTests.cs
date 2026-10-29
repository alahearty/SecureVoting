using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class DepartmentIsolationTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsOfficer = "officer@demo.iaue.edu.ng";
    private const string CsObserver = "auditor@demo.iaue.edu.ng";
    private const string MathOfficer = "officer.math@demo.iaue.edu.ng";
    private const string MathObserver = "observer.math@demo.iaue.edu.ng";
    private const string SuperAdmin = "superadmin@demo.iaue.edu.ng";
    private const string CsStudent = "IAUE/CS/22/001";
    private const string MathStudent = "IAUE/MTH/22/001";

    [Fact]
    public async Task SuperAdminManagesDepartmentsAndDuplicateCodesAreRejected()
    {
        var superAdmin = await Fixture.LoginAsync(SuperAdmin, IntegrationApiFixture.DemoPassword);
        var created = await superAdmin.PostAsJsonAsync("/api/admin/departments", new CreateDepartmentRequest
        {
            Name = "Software Engineering",
            Code = "SWE",
            Faculty = "Faculty of Natural and Applied Sciences"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var department = await ReadAsync<DepartmentDto>(created);
        Assert.Equal("SWE", department.Code);

        var duplicate = await superAdmin.PostAsJsonAsync("/api/admin/departments", new CreateDepartmentRequest
        {
            Name = "Another Software Department",
            Code = "swe"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var deactivate = await superAdmin.PostAsync($"/api/admin/departments/{department.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var inactiveElection = await superAdmin.PostAsJsonAsync("/api/admin/elections", ElectionRequest(department.Id));
        Assert.Equal(HttpStatusCode.Conflict, inactiveElection.StatusCode);

        var activate = await superAdmin.PostAsync($"/api/admin/departments/{department.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
    }

    [Fact]
    public async Task OfficerCanCreateOnlyForAssignedDepartments()
    {
        var csDepartment = await Fixture.DepartmentIdAsync("CSC");
        var mathDepartment = await Fixture.DepartmentIdAsync("MTH");
        var csOfficer = await Fixture.LoginAsync(CsOfficer, IntegrationApiFixture.DemoPassword);
        var mathOfficer = await Fixture.LoginAsync(MathOfficer, IntegrationApiFixture.DemoPassword);

        var forbidden = await csOfficer.PostAsJsonAsync("/api/admin/elections",
            ElectionRequest(mathDepartment, "Cross-department attempt"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var allowed = await csOfficer.PostAsJsonAsync("/api/admin/elections",
            ElectionRequest(csDepartment, "CS scoped election"));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

        var mathAllowed = await mathOfficer.PostAsJsonAsync("/api/admin/elections",
            ElectionRequest(mathDepartment, "Math scoped election"));
        Assert.Equal(HttpStatusCode.Created, mathAllowed.StatusCode);
    }

    [Fact]
    public async Task CrossDepartmentReadsAndWritesAreRejected()
    {
        var mathElectionId = await Fixture.ElectionIdForDepartmentAsync("MTH");
        var csOfficer = await Fixture.LoginAsync(CsOfficer, IntegrationApiFixture.DemoPassword);
        var csObserver = await Fixture.LoginAsync(CsObserver, IntegrationApiFixture.DemoPassword);
        var csStudent = await Fixture.LoginAsync(CsStudent, IntegrationApiFixture.DemoPassword);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await csOfficer.GetAsync($"/api/admin/elections/{mathElectionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csOfficer.GetAsync($"/api/admin/elections/{mathElectionId}/voters")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csOfficer.PostAsJsonAsync($"/api/admin/elections/{mathElectionId}/actions/schedule",
                new ElectionStatusRequest { Reason = "Cross-department attempt" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csObserver.GetAsync($"/api/observer/elections/{mathElectionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csObserver.GetAsync($"/api/result-review/elections/{mathElectionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csStudent.GetAsync($"/api/student/elections/{mathElectionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csStudent.GetAsync($"/api/student/elections/{mathElectionId}/ballot")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await csStudent.GetAsync($"/api/elections/{mathElectionId}/results/published")).StatusCode);

        var csAdminList = await csOfficer.GetFromJsonAsync<IReadOnlyList<ElectionSummaryDto>>(
            "/api/admin/elections", IntegrationApiFixture.JsonOptions);
        var csObserverList = await csObserver.GetFromJsonAsync<IReadOnlyList<ElectionSummaryDto>>(
            "/api/observer/elections", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(csAdminList);
        Assert.NotNull(csObserverList);
        Assert.DoesNotContain(csAdminList, election => election.Id == mathElectionId);
        Assert.DoesNotContain(csObserverList, election => election.Id == mathElectionId);
    }

    [Fact]
    public async Task VoterImportRejectsMismatchedDepartmentWithoutLeakingTokens()
    {
        var csElectionId = await Fixture.DemoElectionIdAsync();
        var csOfficer = await Fixture.LoginAsync(CsOfficer, IntegrationApiFixture.DemoPassword);
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(
            "matric,fullname,email,level,department\n" +
            $"{MathStudent},Math Student,{MathStudent.Replace("/", string.Empty).ToLowerInvariant()}@demo.iaue.edu.ng,300,Mathematics\n"));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(content, "file", "mismatch.csv");
        var response = await csOfficer.PostAsync($"/api/admin/elections/{csElectionId}/voters/import", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<VoterImportResponse>(response);
        Assert.Contains(result.Errors, error => error.Message.Contains("Department", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.ActivationTokens);
        Assert.Equal(0, result.RowsProcessed);
    }

    [Fact]
    public async Task ObserverAuditAndReviewListsAreDepartmentScoped()
    {
        var mathDepartment = await Fixture.DepartmentIdAsync("MTH");
        var mathOfficer = await Fixture.LoginAsync(MathOfficer, IntegrationApiFixture.DemoPassword);
        var mathElection = await mathOfficer.PostAsJsonAsync("/api/admin/elections",
            ElectionRequest(mathDepartment, "Math audit scoped election"));
        Assert.Equal(HttpStatusCode.Created, mathElection.StatusCode);
        var mathElectionId = await ReadIdAsync(mathElection);

        var csObserver = await Fixture.LoginAsync(CsObserver, IntegrationApiFixture.DemoPassword);
        var mathObserver = await Fixture.LoginAsync(MathObserver, IntegrationApiFixture.DemoPassword);
        var csAudit = await csObserver.GetFromJsonAsync<IReadOnlyList<AuditLogDto>>(
            "/api/audit?pageSize=200", IntegrationApiFixture.JsonOptions);
        var mathAudit = await mathObserver.GetFromJsonAsync<IReadOnlyList<AuditLogDto>>(
            "/api/audit?pageSize=200", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(csAudit);
        Assert.NotNull(mathAudit);
        Assert.DoesNotContain(csAudit, entry => entry.EntityId == mathElectionId.ToString());
        Assert.Contains(mathAudit, entry => entry.EntityId == mathElectionId.ToString());

        var csReview = await csObserver.GetFromJsonAsync<IReadOnlyList<ElectionSummaryDto>>(
            "/api/result-review/elections", IntegrationApiFixture.JsonOptions);
        var mathReview = await mathObserver.GetFromJsonAsync<IReadOnlyList<ElectionSummaryDto>>(
            "/api/result-review/elections", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(csReview);
        Assert.NotNull(mathReview);
        Assert.DoesNotContain(csReview, election => election.Id == mathElectionId);
        Assert.Contains(mathReview, election => election.Id == mathElectionId);
    }

    [Fact]
    public async Task ScopedObserverCanVerifyOnlyAssignedDepartmentResult()
    {
        var mathElectionId = await Fixture.ElectionIdForDepartmentAsync("MTH");
        await SetElectionWindowAsync(mathElectionId, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(2));
        var mathOfficer = await Fixture.LoginAsync(MathOfficer, IntegrationApiFixture.DemoPassword);
        var mathObserver = await Fixture.LoginAsync(MathObserver, IntegrationApiFixture.DemoPassword);
        var csObserver = await Fixture.LoginAsync(CsObserver, IntegrationApiFixture.DemoPassword);

        Assert.Equal(HttpStatusCode.NoContent,
            (await mathOfficer.PostAsJsonAsync($"/api/admin/elections/{mathElectionId}/actions/schedule",
                new ElectionStatusRequest { Reason = "Scoped result test." })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await mathOfficer.PostAsJsonAsync($"/api/admin/elections/{mathElectionId}/actions/open",
                new ElectionStatusRequest { Reason = "Scoped result test." })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await mathOfficer.PostAsJsonAsync($"/api/admin/elections/{mathElectionId}/actions/close",
                new ElectionStatusRequest { Reason = "Scoped result test complete." })).StatusCode);
        var review = await mathOfficer.PostAsync($"/api/elections/{mathElectionId}/results/review", null);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var provisional = await ReadAsync<ElectionResultDto>(review);

        var denied = await csObserver.PostAsJsonAsync(
            $"/api/elections/{mathElectionId}/results/{provisional.Id}/verify",
            new VerifyResultRequest { Notes = "Cross-department observation attempt." });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await mathObserver.PostAsJsonAsync(
            $"/api/elections/{mathElectionId}/results/{provisional.Id}/verify",
            new VerifyResultRequest { Notes = "Assigned department observation." });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task IndependentElectionsEnforceEligibilityAndOneVote()
    {
        var csElectionId = await Fixture.DemoElectionIdAsync();
        var mathElectionId = await Fixture.ElectionIdForDepartmentAsync("MTH");
        await SetElectionWindowAsync(csElectionId, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(2));
        await SetElectionWindowAsync(mathElectionId, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(2));

        var csOfficer = await Fixture.LoginAsync(CsOfficer, IntegrationApiFixture.DemoPassword);
        var mathOfficer = await Fixture.LoginAsync(MathOfficer, IntegrationApiFixture.DemoPassword);
        await ScheduleAndOpenAsync(csOfficer, csElectionId, "CS independent election.");
        await ScheduleAndOpenAsync(mathOfficer, mathElectionId, "Math independent election.");

        var csStudent = await Fixture.LoginAsync(CsStudent, IntegrationApiFixture.DemoPassword);
        var mathStudent = await Fixture.LoginAsync(MathStudent, IntegrationApiFixture.DemoPassword);
        var csBallot = await csStudent.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{csElectionId}/ballot", IntegrationApiFixture.JsonOptions);
        var mathBallot = await mathStudent.GetFromJsonAsync<BallotDto>(
            $"/api/student/elections/{mathElectionId}/ballot", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(csBallot);
        Assert.NotNull(mathBallot);
        Assert.NotEmpty(csBallot.Positions);
        Assert.NotEmpty(mathBallot.Positions);

        var csCross = await csStudent.GetAsync($"/api/student/elections/{mathElectionId}/ballot");
        Assert.Equal(HttpStatusCode.Forbidden, csCross.StatusCode);

        var csSubmit = await csStudent.PostAsJsonAsync($"/api/student/elections/{csElectionId}/ballots",
            BallotFor(csBallot));
        var mathSubmit = await mathStudent.PostAsJsonAsync($"/api/student/elections/{mathElectionId}/ballots",
            BallotFor(mathBallot));
        Assert.Equal(HttpStatusCode.Created, csSubmit.StatusCode);
        Assert.Equal(HttpStatusCode.Created, mathSubmit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await csStudent.PostAsJsonAsync($"/api/student/elections/{csElectionId}/ballots",
                BallotFor(csBallot))).StatusCode);

        var counts = await Fixture.QueryAsync(async db => new
        {
            CsBallots = await db.Ballots.CountAsync(b => b.ElectionId == csElectionId),
            MathBallots = await db.Ballots.CountAsync(b => b.ElectionId == mathElectionId)
        });
        Assert.Equal(1, counts.CsBallots);
        Assert.Equal(1, counts.MathBallots);
    }

    private static CreateElectionRequest ElectionRequest(Guid departmentId, string title = "Scoped synthetic election") =>
        new()
        {
            Title = title,
            Description = "Synthetic scoped election for isolation tests.",
            AcademicSession = "2026/2027",
            DepartmentId = departmentId,
            NominationOpensAt = DateTimeOffset.UtcNow.AddHours(-2),
            NominationClosesAt = DateTimeOffset.UtcNow.AddHours(-1),
            OpensAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ClosesAt = DateTimeOffset.UtcNow.AddHours(2),
            AllowAbstention = true,
            NominationRequiresApproval = true
        };

    private static CastBallotRequest BallotFor(BallotDto ballot) =>
        new()
        {
            Positions = ballot.Positions.Select(position => new PositionChoiceRequest
            {
                PositionId = position.Id,
                CandidateIds = [position.Candidates[0].Id]
            }).ToList()
        };

    private async Task SetElectionWindowAsync(Guid electionId, DateTime opensAt, DateTime closesAt)
    {
        await Fixture.QueryAsync(async db =>
        {
            var election = await db.Elections.SingleAsync(e => e.Id == electionId);
            election.OpensAtUtc = opensAt;
            election.ClosesAtUtc = closesAt;
            election.NominationOpensAtUtc = opensAt.AddHours(-2);
            election.NominationClosesAtUtc = opensAt.AddHours(-1);
            return await db.SaveChangesAsync();
        });
    }

    private static async Task ScheduleAndOpenAsync(HttpClient officer, Guid electionId, string reason)
    {
        var schedule = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/schedule",
            new ElectionStatusRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.NoContent, schedule.StatusCode);
        var open = await officer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/open",
            new ElectionStatusRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.NoContent, open.StatusCode);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var document = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        return document!["id"];
    }
}
