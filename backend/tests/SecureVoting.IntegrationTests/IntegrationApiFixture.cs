using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SecureVoting.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationApiFixture>
{
    public const string Name = "securevote-api";
}

public sealed class IntegrationApiFixture : IAsyncLifetime
{
    public const string DemoPassword = "DemoPass!2026Secure";
    private PostgreSqlContainer? _container;

    private WebApplicationFactory<Program>? _factory;
    private string _connectionString = string.Empty;
    private string _dataProtectionPath = string.Empty;
    private string _photoPath = string.Empty;

    public HttpClient CreateAnonymousClient()
    {
        var client = (_factory ?? throw new InvalidOperationException("Fixture is not initialized."))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
        _activeClients.Add(client);
        return client;
    }

    public async Task<HttpClient> LoginAsync(string identifier, string password)
    {
        var client = CreateAnonymousClient();
        await AttachCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = identifier,
            Password = password
        });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Login failed for {identifier}: {(int)response.StatusCode} {body}");
        }

        await AttachCsrfAsync(client);
        return client;
    }

    public async Task AttachCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"CSRF endpoint failed: {(int)response.StatusCode} {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload?.Token ?? string.Empty);
    }

    public async Task ResetDatabaseAsync()
    {
        foreach (var client in _activeClients)
        {
            client.Dispose();
        }

        _activeClients.Clear();
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS public CASCADE");
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA public");
        await db.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.InitializeAsync();
    }

    public async Task<Guid> DemoElectionIdAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Elections
            .Where(e => e.Title == DatabaseSeeder.DemoElectionTitle)
            .Select(e => e.Id)
            .SingleAsync();
    }

    public async Task<Guid> DepartmentIdAsync(string code, CancellationToken cancellationToken = default)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Departments
            .Where(d => d.CodeNormalized == code.ToUpperInvariant())
            .Select(d => d.Id)
            .SingleAsync(cancellationToken);
    }

    public async Task<Guid> ElectionIdForDepartmentAsync(string departmentCode, CancellationToken cancellationToken = default)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Elections
            .Include(e => e.DepartmentRef)
            .Where(e => e.DepartmentRef.CodeNormalized == departmentCode.ToUpperInvariant())
            .OrderBy(e => e.Title)
            .Select(e => e.Id)
            .FirstAsync(cancellationToken);
    }

    public async Task<Guid> CandidateIdAsync(string displayName)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Candidates
            .Where(c => c.DisplayName == displayName)
            .Select(c => c.Id)
            .FirstAsync();
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private readonly List<HttpClient> _activeClients = [];

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        var externalConnection = Environment.GetEnvironmentVariable("SECUREVOTING_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(externalConnection))
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = externalConnection };
            var database = builder.TryGetValue("Database", out var databaseValue)
                ? databaseValue?.ToString()
                : null;
            if (string.IsNullOrWhiteSpace(database) ||
                !database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SECUREVOTING_TEST_CONNECTION must point to a dedicated database whose name ends with '_test'.");
            }

            _connectionString = externalConnection;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("securevote")
                .WithUsername("securevote")
                .WithPassword("securevote_test_password")
                .WithEnvironment("POSTGRES_USER", "securevote")
                .WithEnvironment("POSTGRES_PASSWORD", "securevote_test_password")
                .WithEnvironment("POSTGRES_DB", "securevote")
                .Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        _dataProtectionPath = Path.Combine(Path.GetTempPath(), "securevote-dp-" + Guid.NewGuid().ToString("N"));
        _photoPath = Path.Combine(Path.GetTempPath(), "securevote-photos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataProtectionPath);
        Directory.CreateDirectory(_photoPath);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Seed__Enabled", "true");
        Environment.SetEnvironmentVariable("Seed__DemoPassword", DemoPassword);
        Environment.SetEnvironmentVariable("DataProtection__KeysPath", _dataProtectionPath);
        Environment.SetEnvironmentVariable("Storage__CandidatePhotosPath", _photoPath);
        Environment.SetEnvironmentVariable("RateLimit__LoginPermitLimit", "1000");
        Environment.SetEnvironmentVariable("RateLimit__BallotPermitLimit", "100");
        Environment.SetEnvironmentVariable("Email__Enabled", "false");
        Environment.SetEnvironmentVariable("Security__UseHttpsRedirection", "false");
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = _connectionString,
                        ["Seed:Enabled"] = "true",
                        ["Seed:DemoPassword"] = DemoPassword,
                        ["App:FrontendBaseUrl"] = "http://localhost:5173",
                        ["Email:Enabled"] = "false",
                        ["DataProtection:KeysPath"] = _dataProtectionPath,
                        ["Storage:CandidatePhotosPath"] = _photoPath,
                        ["RateLimit:LoginPermitLimit"] = "1000",
                        ["RateLimit:BallotPermitLimit"] = "100",
                        ["Security:UseHttpsRedirection"] = "false"
                    });
                });
            });
        _ = _factory.Services;
    }

    public async Task DisposeAsync()
    {
        foreach (var client in _activeClients)
        {
            client.Dispose();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        try
        {
            if (Directory.Exists(_dataProtectionPath))
            {
                Directory.Delete(_dataProtectionPath, recursive: true);
            }

            if (Directory.Exists(_photoPath))
            {
                Directory.Delete(_photoPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    public HttpClient CreateTrackedClient()
    {
        var client = CreateAnonymousClient();
        _activeClients.Add(client);
        return client;
    }
}

public abstract class IntegrationTestBase(IntegrationApiFixture fixture) : IAsyncLifetime
{
    protected IntegrationApiFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static string Json(object value) => JsonSerializer.Serialize(value, IntegrationApiFixture.JsonOptions);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var value = JsonSerializer.Deserialize<T>(body, IntegrationApiFixture.JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException($"Response did not contain {typeof(T).Name}: {body}");
        }

        return value;
    }
}
