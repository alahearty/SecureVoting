using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class CandidateWorkflowTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OfficerEmail = "officer@demo.iaue.edu.ng";
    private const string StudentNumber = "IAUE/CS/22/001";

    [Fact]
    public async Task StudentCanNominateAndOfficerCanApproveCandidate()
    {
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var now = DateTimeOffset.UtcNow;
        var created = await officer.PostAsJsonAsync("/api/admin/elections", new CreateElectionRequest
        {
            Title = "Candidate workflow election",
            Description = "Synthetic nomination test",
            AcademicSession = "2026/2027",
            DepartmentId = await Fixture.DepartmentIdAsync("CSC"),
            NominationOpensAt = now.AddHours(-1),
            NominationClosesAt = now.AddHours(1),
            OpensAt = now.AddHours(2),
            ClosesAt = now.AddHours(3),
            AllowAbstention = true,
            TieHandling = SecureVoting.Domain.TieHandling.RunoffRequired
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var electionId = await ReadIdAsync(created);

        var position = await officer.PostAsJsonAsync($"/api/admin/elections/{electionId}/positions", new CreatePositionRequest
        {
            Title = "President",
            Description = "Candidate workflow position",
            DisplayOrder = 1,
            SeatCount = 1,
            MaxSelections = 1
        });
        Assert.Equal(HttpStatusCode.Created, position.StatusCode);
        var positionId = await ReadIdAsync(position);

        using var form = new MultipartFormDataContent();
        using var csv = new ByteArrayContent(Encoding.UTF8.GetBytes(
            "matric,fullname,email,level,department\n" +
            $"IAUE/CS/22/001,Adaeze N. Okafor,adaeze001@demo.iaue.edu.ng,300,Computer Science\n"));
        csv.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(csv, "file", "voters.csv");
        var import = await officer.PostAsync($"/api/admin/elections/{electionId}/voters/import", form);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var imported = await ReadAsync<VoterImportResponse>(import);
        Assert.Equal(1, imported.RowsProcessed);

        var student = await Fixture.LoginAsync(StudentNumber, IntegrationApiFixture.DemoPassword);
        var nomination = await student.PostAsJsonAsync("/api/candidate/nominations", new CandidateNominationRequest
        {
            ElectionId = electionId,
            PositionId = positionId,
            Manifesto = "I will serve the department transparently and communicate decisions clearly."
        });
        Assert.Equal(HttpStatusCode.Created, nomination.StatusCode);

        var candidates = await officer.GetFromJsonAsync<IReadOnlyList<CandidateAdminDto>>(
            $"/api/admin/elections/{electionId}/candidates", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(candidates);
        var candidate = Assert.Single(candidates);
        Assert.Equal("Pending", candidate.Status.ToString());

        var approve = await officer.PostAsync(
            $"/api/admin/elections/{electionId}/candidates/{candidate.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var approved = await officer.GetFromJsonAsync<IReadOnlyList<CandidateAdminDto>>(
            $"/api/admin/elections/{electionId}/candidates", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(approved);
        Assert.Equal("Approved", Assert.Single(approved).Status.ToString());

        var candidateClient = await Fixture.LoginAsync(StudentNumber, IntegrationApiFixture.DemoPassword);
        var me = await candidateClient.GetFromJsonAsync<MeResponse>("/api/auth/me", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(me);
        Assert.Contains("Candidate", me.Roles);
        var profile = await candidateClient.GetFromJsonAsync<CandidateProfileDto>(
            "/api/candidate/profile", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("Approved", profile.Status.ToString());
        Assert.Equal(electionId, profile.ElectionId);
    }

    [Fact]
    public async Task StudentCannotUploadPhotoForAnotherCandidate()
    {
        var candidateId = await Fixture.CandidateIdAsync("Chinedu M. Eze");
        var student = await Fixture.LoginAsync(StudentNumber, IntegrationApiFixture.DemoPassword);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "photo", "not-mine.png");
        var response = await student.PostAsync($"/api/candidate/nominations/{candidateId}/photo", form);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var photo = await Fixture.QueryAsync(async db =>
            (await db.Candidates.AsNoTracking().SingleAsync(c => c.Id == candidateId)).PhotoFileName);
        Assert.Null(photo);
    }
    [Fact]
    public async Task CandidatePhotoUploadAcceptsRasterAndRejectsDisguisedContent()
    {
        var candidateId = await Fixture.CandidateIdAsync("Chinedu M. Eze");
        var candidate = await Fixture.LoginAsync("IAUE/CS/22/002", IntegrationApiFixture.DemoPassword);
        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
        };
        using var goodForm = new MultipartFormDataContent();
        using var goodContent = new ByteArrayContent(png);
        goodContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        goodForm.Add(goodContent, "photo", "candidate.png");
        var good = await candidate.PostAsync($"/api/candidate/nominations/{candidateId}/photo", goodForm);
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);

        var photo = await candidate.GetAsync($"/api/candidates/{candidateId}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await photo.Content.ReadAsByteArrayAsync());

        using var badForm = new MultipartFormDataContent();
        using var badContent = new ByteArrayContent(Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>"));
        badContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        badForm.Add(badContent, "photo", "disguised.png");
        var bad = await candidate.PostAsync($"/api/candidate/nominations/{candidateId}/photo", badForm);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
