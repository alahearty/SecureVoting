using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

var baseUrl = Environment.GetEnvironmentVariable("SECUREVOTE_LOAD_URL") ?? "http://localhost:8092";
var demoPassword = Environment.GetEnvironmentVariable("SECUREVOTE_LOAD_PASSWORD") ?? "DemoPass!2026Secure";
var voterCount = int.TryParse(Environment.GetEnvironmentVariable("SECUREVOTE_LOAD_VOTERS"), out var configuredVoters)
    ? configuredVoters
    : 300;
var concurrency = int.TryParse(Environment.GetEnvironmentVariable("SECUREVOTE_LOAD_CONCURRENCY"), out var configuredConcurrency)
    ? configuredConcurrency
    : 32;
const string voterPassword = "Load!Pass2026";
var runId = DateTime.UtcNow.ToString("HHmmssfff", CultureInfo.InvariantCulture);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

Console.WriteLine($"SecureVote load test: {baseUrl}, voters={voterCount}, concurrency={concurrency}, run={runId}");

using var officer = new Session(baseUrl);
await officer.LoginAsync("officer@demo.iaue.edu.ng", demoPassword);
var electionId = await SelectDemoElectionAsync(officer);
Console.WriteLine($"Election={electionId}");

var csv = new StringBuilder("matric,fullname,email,level,department\n");
var voters = new List<LoadVoter>(voterCount);
for (var i = 1; i <= voterCount; i++)
{
    var matric = $"LOAD/{runId}/{i:000}";
    var email = $"load-{runId}-{i:000}@demo.iaue.edu.ng";
    voters.Add(new LoadVoter(matric, email));
    csv.AppendLine($"{matric},Load Tester {i:000},{email},300,Computer Science");
}

var import = await officer.PostCsvAsync($"/api/admin/elections/{electionId}/voters/import", csv.ToString());
if (!import.IsSuccessStatusCode)
{
    throw new InvalidOperationException($"Import failed: {(int)import.StatusCode} {await import.Content.ReadAsStringAsync()}");
}

using var importDocument = JsonDocument.Parse(await import.Content.ReadAsStringAsync());
var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var token in importDocument.RootElement.GetProperty("activationTokens").EnumerateArray())
{
    tokens[token.GetProperty("matricNumber").GetString()!] = token.GetProperty("token").GetString()!;
}

var schedule = await officer.PostJsonAsync(
    $"/api/admin/elections/{electionId}/actions/schedule",
    new { reason = "Load test schedule." });
Ensure(schedule, "schedule");
var open = await officer.PostJsonAsync(
    $"/api/admin/elections/{electionId}/actions/open",
    new { reason = "Load test opening." });
Ensure(open, "open");

using var demo = new Session(baseUrl);
await demo.LoginAsync("IAUE/CS/22/001", demoPassword);
using var ballotDocument = JsonDocument.Parse(await demo.GetStringAsync($"/api/student/elections/{electionId}/ballot"));
var positions = ParsePositions(ballotDocument.RootElement);
Console.WriteLine($"Positions={positions.Count}, candidate choices={positions.Sum(position => position.CandidateIds.Count())}");

var activationLatencies = new ConcurrentBag<double>();
var activationFailures = new ConcurrentBag<string>();
var activationSemaphore = new SemaphoreSlim(concurrency);
var sessions = new ConcurrentBag<Session>();

var activationTasks = voters.Select(async voter =>
{
    await activationSemaphore.WaitAsync();
    try
    {
        var session = new Session(baseUrl);
        var stopwatch = Stopwatch.StartNew();
        var response = await session.PostJsonAsync(
            "/api/auth/activate",
            new
            {
                matricNumber = voter.Matric,
                token = tokens[voter.Matric],
                password = voterPassword,
                confirmPassword = voterPassword
            },
            includeCsrf: true);
        stopwatch.Stop();
        if (!response.IsSuccessStatusCode)
        {
            activationFailures.Add($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            session.Dispose();
            return;
        }

        await session.RefreshCsrfAsync();
        activationLatencies.Add(stopwatch.Elapsed.TotalMilliseconds);
        sessions.Add(session);
    }
    catch (Exception ex)
    {
        activationFailures.Add(ex.Message);
    }
    finally
    {
        activationSemaphore.Release();
    }
});
await Task.WhenAll(activationTasks);
Console.WriteLine($"Activated={sessions.Count}, activationFailures={activationFailures.Count}");

var submissionLatencies = new ConcurrentBag<double>();
var submissionFailures = new ConcurrentBag<string>();
var accepted = 0;
var submitSemaphore = new SemaphoreSlim(concurrency);
var submitTasks = sessions.Select(async session =>
{
    await submitSemaphore.WaitAsync();
    try
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await session.PostJsonAsync(
            $"/api/student/elections/{electionId}/ballots",
            new
            {
                positions = positions.Select(position => new
                {
                    positionId = position.PositionId,
                    candidateIds = new[] { position.CandidateIds[0] }
                })
            });
        stopwatch.Stop();
        if (response.StatusCode == HttpStatusCode.Created)
        {
            Interlocked.Increment(ref accepted);
            submissionLatencies.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        else
        {
            submissionFailures.Add($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }
    catch (Exception ex)
    {
        submissionFailures.Add(ex.Message);
    }
    finally
    {
        submitSemaphore.Release();
    }
});
await Task.WhenAll(submitTasks);

var duplicateRejected = false;
var firstSession = sessions.FirstOrDefault();
if (firstSession is not null)
{
    var duplicate = await firstSession.PostJsonAsync(
        $"/api/student/elections/{electionId}/ballots",
        new
        {
            positions = positions.Select(position => new
            {
                positionId = position.PositionId,
                candidateIds = new[] { position.CandidateIds[0] }
            })
        });
    duplicateRejected = duplicate.StatusCode == HttpStatusCode.Conflict;
}

var close = await officer.PostJsonAsync(
    $"/api/admin/elections/{electionId}/actions/close",
    new { reason = "Load test completed." });
Ensure(close, "close");
var review = await officer.PostJsonAsync($"/api/elections/{electionId}/results/review", new { });
Ensure(review, "result review");
using var resultDocument = JsonDocument.Parse(await review.Content.ReadAsStringAsync());
var totalBallots = resultDocument.RootElement.GetProperty("totalBallots").GetInt32();
var turnout = resultDocument.RootElement.GetProperty("turnoutPercentage").GetDecimal();
var resultSum = 0;
foreach (var position in resultDocument.RootElement.GetProperty("positions").EnumerateArray())
{
    foreach (var candidate in position.GetProperty("candidates").EnumerateArray())
    {
        resultSum += candidate.GetProperty("votes").GetInt32();
    }
}

var latencies = submissionLatencies.ToArray();
var activationArray = activationLatencies.ToArray();
var p50 = Percentile(latencies, 50);
var p95 = Percentile(latencies, 95);
var p99 = Percentile(latencies, 99);
var errorCount = activationFailures.Count + submissionFailures.Count + (duplicateRejected ? 0 : 1);
Console.WriteLine("=== RESULT ===");
Console.WriteLine($"TimestampUtc={DateTime.UtcNow:O}");
Console.WriteLine($"VotersRequested={voterCount}");
Console.WriteLine($"Activated={sessions.Count}");
Console.WriteLine($"Accepted={accepted}");
Console.WriteLine($"SubmissionFailures={submissionFailures.Count}");
Console.WriteLine($"ActivationFailures={activationFailures.Count}");
Console.WriteLine($"DuplicateRejected={duplicateRejected}");
Console.WriteLine($"TotalBallotsInResult={totalBallots}");
Console.WriteLine($"ResultCandidateVoteSum={resultSum}");
Console.WriteLine($"ExpectedResultCandidateVoteSum={accepted * positions.Count}");
Console.WriteLine($"TurnoutPercent={turnout.ToString("0.00", CultureInfo.InvariantCulture)}");
Console.WriteLine($"ActivationMsP50={Percentile(activationArray, 50):0.0}");
Console.WriteLine($"ActivationMsP95={Percentile(activationArray, 95):0.0}");
Console.WriteLine($"SubmissionMsP50={p50:0.0}");
Console.WriteLine($"SubmissionMsP95={p95:0.0}");
Console.WriteLine($"SubmissionMsP99={p99:0.0}");
Console.WriteLine($"ErrorCount={errorCount}");
foreach (var failure in submissionFailures.Take(5))
{
    Console.WriteLine($"SubmissionFailure={failure}");
}
foreach (var failure in activationFailures.Take(5))
{
    Console.WriteLine($"ActivationFailure={failure}");
}

var success = sessions.Count == voterCount
              && accepted == voterCount
              && duplicateRejected
              && totalBallots == accepted
              && resultSum == accepted * positions.Count
              && errorCount == 0;
Console.WriteLine($"SUCCESS={success}");
Environment.ExitCode = success ? 0 : 1;

static async Task<Guid> SelectDemoElectionAsync(Session officer)
{
    using var document = JsonDocument.Parse(await officer.GetStringAsync("/api/admin/elections"));
    foreach (var election in document.RootElement.EnumerateArray())
    {
        if (election.GetProperty("status").GetString() == "Draft" &&
            election.GetProperty("title").GetString()?.Contains("Computer Science Departmental Election") == true)
        {
            return election.GetProperty("id").GetGuid();
        }
    }

    throw new InvalidOperationException("A fresh Draft demo election was not found. Start with a fresh Demo database.");
}

static List<LoadPosition> ParsePositions(JsonElement ballot)
{
    return ballot.GetProperty("positions").EnumerateArray()
        .Select(position => new LoadPosition(
            position.GetProperty("id").GetGuid(),
            position.GetProperty("candidates").EnumerateArray()
                .Select(candidate => candidate.GetProperty("id").GetGuid())
                .ToArray()))
        .ToList();
}

static void Ensure(HttpResponseMessage response, string operation)
{
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"{operation} failed: {(int)response.StatusCode} {response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}");
    }
}

static double Percentile(double[] values, int percentile)
{
    if (values.Length == 0)
    {
        return 0;
    }

    var sorted = values.OrderBy(value => value).ToArray();
    var index = (int)Math.Ceiling((percentile / 100d) * sorted.Length) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
}

internal sealed record LoadVoter(string Matric, string Email);

internal sealed record LoadPosition(Guid PositionId, Guid[] CandidateIds);

internal sealed class Session : IDisposable
{
    private readonly HttpClient _client;
    private string _csrf = string.Empty;

    public Session(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task LoginAsync(string identifier, string password)
    {
        var response = await PostJsonAsync("/api/auth/login", new { identifier, password });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Login failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        await RefreshCsrfAsync();
    }

    public async Task RefreshCsrfAsync()
    {
        using var response = await _client.GetAsync("api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        _csrf = document.RootElement.GetProperty("token").GetString()!;
    }

    public async Task<string> GetStringAsync(string path)
    {
        using var response = await _client.GetAsync(path.TrimStart('/'));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<HttpResponseMessage> PostJsonAsync(string path, object body, bool includeCsrf = true)
    {
        if (includeCsrf && string.IsNullOrWhiteSpace(_csrf))
        {
            await RefreshCsrfAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Encoding.UTF8,
                "application/json")
        };
        if (includeCsrf)
        {
            request.Headers.Add("X-CSRF-TOKEN", _csrf);
        }

        return await _client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PostCsvAsync(string path, string csv)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(content, "file", "load-voters.csv");
        using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/')) { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", _csrf);
        return await _client.SendAsync(request);
    }

    public void Dispose() => _client.Dispose();
}
