using System.Net;
using System.Net.Http.Json;
using SecureVoting.Application.Contracts;
using Xunit;

namespace SecureVoting.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class DashboardAndAnnouncementTests(IntegrationApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OfficerEmail = "officer@demo.iaue.edu.ng";
    private const string AuditorEmail = "auditor@demo.iaue.edu.ng";
    private const string StudentId = "IAUE/CS/22/001";
    private const string CandidateId = "IAUE/CS/22/002";

    [Fact]
    public async Task RoleDashboardsLoadAndAnnouncementsReachStudents()
    {
        var officer = await Fixture.LoginAsync(OfficerEmail, IntegrationApiFixture.DemoPassword);
        var electionId = await Fixture.DemoElectionIdAsync();
        var announcement = await officer.PostAsJsonAsync("/api/admin/announcements", new CreateAnnouncementRequest
        {
            Title = "Dashboard announcement",
            Body = "This is a synthetic dashboard announcement.",
            Audience = SecureVoting.Domain.AnnouncementAudience.EligibleVoters,
            ElectionId = electionId
        });
        Assert.Equal(HttpStatusCode.Created, announcement.StatusCode);

        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var studentDashboard = await student.GetFromJsonAsync<StudentDashboardDto>(
            "/api/dashboard/student", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(studentDashboard);
        Assert.Contains(studentDashboard.Announcements, a => a.Title == "Dashboard announcement");
        Assert.NotEmpty(studentDashboard.Elections);

        var adminDashboard = await officer.GetFromJsonAsync<AdminDashboardDto>(
            "/api/dashboard/admin", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(adminDashboard);
        Assert.NotEmpty(adminDashboard.Metrics);

        var observer = await Fixture.LoginAsync(AuditorEmail, IntegrationApiFixture.DemoPassword);
        var observerDashboard = await observer.GetFromJsonAsync<ObserverDashboardDto>(
            "/api/dashboard/observer", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(observerDashboard);
        Assert.NotEmpty(observerDashboard.Metrics);

        var candidate = await Fixture.LoginAsync(CandidateId, IntegrationApiFixture.DemoPassword);
        var candidateDashboard = await candidate.GetFromJsonAsync<CandidateDashboardDto>(
            "/api/dashboard/candidate", IntegrationApiFixture.JsonOptions);
        Assert.NotNull(candidateDashboard);
        Assert.NotNull(candidateDashboard.Nomination);
    }

    [Fact]
    public async Task StudentCannotReadAdminDashboard()
    {
        var student = await Fixture.LoginAsync(StudentId, IntegrationApiFixture.DemoPassword);
        var response = await student.GetAsync("/api/dashboard/admin");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
