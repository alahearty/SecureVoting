using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class VoterProvisioningTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OfficerEmail = "officer@demo.iaue.edu.ng";

    [Fact]
    public async Task OfficerCanImportVoterAndStudentCanActivateAndAuthenticate()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var csv = "matric,fullname,email,level,department\n" +
                  "IAUE/CS/22/099,Test Imported Student,imported099@demo.iaue.edu.ng,300,Computer Science\n";
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(content, "file", "voters.csv");

        var import = await officer.PostAsync(
            $"/api/admin/elections/{electionId}/voters/import", form);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var imported = await ReadAsync<VoterImportResponse>(import);
        Assert.Equal(1, imported.RowsProcessed);
        var credential = Assert.Single(imported.ActivationTokens);
        Assert.Equal("IAUE/CS/22/099", credential.MatricNumber);
        Assert.False(string.IsNullOrWhiteSpace(credential.Token));

        var anonymous = Fixture.CreateAnonymousClient();
        await Fixture.AttachCsrfAsync(anonymous);
        var activation = await anonymous.PostAsJsonAsync("/api/auth/activate", new ActivateAccountRequest
        {
            MatricNumber = credential.MatricNumber,
            Token = credential.Token,
            Password = "Imported!Pass2026",
            ConfirmPassword = "Imported!Pass2026"
        });
        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        var me = await ReadAsync<MeResponse>(activation);
        Assert.Contains("Student", me.Roles);
        Assert.Equal("IAUE/CS/22/099", me.MatricNumber);
    }

    [Fact]
    public async Task ActivationTokenCannotBeReused()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        const string matric = "E2E/CS/26/097";
        var csv = "matric,fullname,email,level,department\n" +
                  $"{matric},Token Reuse Test,token-reuse@demo.iaue.edu.ng,300,Computer Science\n";
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(content, "file", "voters.csv");
        var import = await officer.PostAsync($"/api/admin/elections/{electionId}/voters/import", form);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var imported = await ReadAsync<VoterImportResponse>(import);
        var credential = Assert.Single(imported.ActivationTokens);

        var firstClient = Fixture.CreateAnonymousClient();
        await Fixture.AttachCsrfAsync(firstClient);
        var first = await firstClient.PostAsJsonAsync("/api/auth/activate", new ActivateAccountRequest
        {
            MatricNumber = matric,
            Token = credential.Token,
            Password = "TokenReuse!2026",
            ConfirmPassword = "TokenReuse!2026"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var secondClient = Fixture.CreateAnonymousClient();
        await Fixture.AttachCsrfAsync(secondClient);
        var second = await secondClient.PostAsJsonAsync("/api/auth/activate", new ActivateAccountRequest
        {
            MatricNumber = matric,
            Token = credential.Token,
            Password = "TokenReuse!2026",
            ConfirmPassword = "TokenReuse!2026"
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
    [Fact]
    public async Task ExpiredActivationTokenIsRejected()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        const string matric = "E2E/CS/26/098";
        var csv = "matric,fullname,email,level,department\n" +
                  $"{matric},Expired Token Test,expired-token@demo.iaue.edu.ng,300,Computer Science\n";
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(content, "file", "voters.csv");
        var import = await officer.PostAsync($"/api/admin/elections/{electionId}/voters/import", form);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var imported = await ReadAsync<VoterImportResponse>(import);
        var credential = Assert.Single(imported.ActivationTokens);

        await Fixture.QueryAsync(async db =>
        {
            var student = await db.Students.SingleAsync(s => s.MatricNumberNormalized == matric);
            var token = await db.StudentActivationTokens
                .Where(t => t.StudentId == student.Id && t.UsedAtUtc == null)
                .OrderByDescending(t => t.CreatedAtUtc)
                .FirstAsync();
            token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        var anonymous = Fixture.CreateAnonymousClient();
        await Fixture.AttachCsrfAsync(anonymous);
        var activation = await anonymous.PostAsJsonAsync("/api/auth/activate", new ActivateAccountRequest
        {
            MatricNumber = matric,
            Token = credential.Token,
            Password = "Expired!Pass2026",
            ConfirmPassword = "Expired!Pass2026"
        });
        Assert.Equal(HttpStatusCode.BadRequest, activation.StatusCode);
    }

    [Fact]
    public async Task InvalidImportDoesNotCreateOrTruncateVoterData()
    {
        var electionId = await Fixture.DemoElectionIdAsync();
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var before = await Fixture.QueryAsync(db => db.Students.CountAsync());
        var csv = "matric,fullname,email,level,department\n" +
                  "BAD,Another Student,not-an-email,300,Computer Science\n";
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(content, "file", "voters.csv");

        var import = await officer.PostAsync(
            $"/api/admin/elections/{electionId}/voters/import", form);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var result = await ReadAsync<VoterImportResponse>(import);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(before, await Fixture.QueryAsync(db => db.Students.CountAsync()));
    }
}
