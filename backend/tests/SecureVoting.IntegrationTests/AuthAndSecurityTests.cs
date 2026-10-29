using System.Net;
using System.Net.Http.Json;
using SecureVoting.Application.Contracts;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthAndSecurityTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string StudentId = "IAUE/CS/22/001";
    private const string OfficerEmail = "officer@demo.iaue.edu.ng";
    private const string AuditorEmail = "auditor@demo.iaue.edu.ng";

    [Fact]
    public async Task StudentCanSignInAndReadOwnProfile()
    {
        var client = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(me);
        Assert.Contains("Student", me.Roles);
        Assert.Equal(StudentId, me.MatricNumber);
        Assert.True(me.HasStudentProfile);
        Assert.False(me.HasCandidateProfile);
    }

    [Fact]
    public async Task LoginRequiresCsrfToken()
    {
        var client = Fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = StudentId,
            Password = IntegrationApiFixture.DemoPassword
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUserCannotReadAdminData()
    {
        var client = Fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/api/admin/elections");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CandidateCannotAccessElectoralAdministration()
    {
        var candidate = await Fixture.LoginAsync("IAUE/CS/22/002", IntegrationApiFixture.DemoPassword);
        var response = await candidate.GetAsync("/api/admin/elections");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ObserverCannotOpenElections()
    {
        var observer = await Fixture.LoginAsync(AuditorEmail, IntegrationApiFixture.DemoPassword);
        var electionId = await Fixture.DemoElectionIdAsync();
        var response = await observer.PostAsJsonAsync(
            $"/api/admin/elections/{electionId}/actions/open",
            new ElectionStatusRequest { Reason = "Unauthorised attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffRolesAreAssignedWithLeastPrivilege()
    {
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var officerMe = await officer.GetFromJsonAsync<MeResponse>("/api/auth/me", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(officerMe);
        Assert.Contains("ElectoralOfficer", officerMe.Roles);
        Assert.DoesNotContain("SuperAdministrator", officerMe.Roles);

        var auditor = await Fixture.LoginAsync(AuditorEmail, IntegrationApiFixture.DemoPassword);
        var auditorMe = await auditor.GetFromJsonAsync<MeResponse>("/api/auth/me", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(auditorMe);
        Assert.Contains("Observer", auditorMe.Roles);
        Assert.DoesNotContain("ManageElections", auditorMe.Roles);
    }
}
