using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Api.Infrastructure;
using SecureVoting.Application.Abstractions;
using SecureVoting.Application.Configuration;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Announcements;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Ballots;
using SecureVoting.Infrastructure.Candidates;
using SecureVoting.Infrastructure.Configuration;
using SecureVoting.Infrastructure.Dashboard;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Email;
using SecureVoting.Infrastructure.Elections;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;
using SecureVoting.Infrastructure.Results;
using SecureVoting.Infrastructure.Settings;
using SecureVoting.Infrastructure.Storage;
using SecureVoting.Infrastructure.Students;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");
var isDevelopment = builder.Environment.IsDevelopment() || isTesting;
var secureCookies = !isDevelopment;

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=securevote;Username=securevote;Password=securevote_dev_local";

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024;
});

builder.Logging.ClearProviders();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
if (isDevelopment)
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
}
else
{
    builder.Logging.AddJsonConsole();
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.PostConfigure<StorageOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.CandidatePhotosPath))
    {
        options.CandidatePhotosPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "CandidatePhotos");
    }
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    if (isTesting)
    {
        options.EnableSensitiveDataLogging(false);
    }
});
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<AuditVerifier>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<StudentImportService>();
builder.Services.AddScoped<ActivationService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<ElectionReadService>();
builder.Services.AddScoped<ElectionAdminService>();
builder.Services.AddScoped<CandidateService>();
builder.Services.AddScoped<BallotService>();
builder.Services.AddScoped<ResultService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<DepartmentAccessService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddSingleton<ICandidatePhotoStore, CandidatePhotoStore>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<ElectionLifecycleWorker>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+/";
    options.SignIn.RequireConfirmedAccount = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
    .AddRoles<IdentityRole<Guid>>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromMinutes(
        builder.Configuration.GetValue("App:PasswordResetMinutes", 30));
});

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SecureVote.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(2);
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("StudentPortal", policy => policy.RequireRole(RoleNames.Student));
    options.AddPolicy("CandidatePortal", policy => policy.RequireRole(RoleNames.Student, RoleNames.Candidate));
    options.AddPolicy("ManageElections", policy =>
        policy.RequireRole(RoleNames.ElectoralOfficer, RoleNames.SuperAdministrator));
    options.AddPolicy("ReviewResults", policy => policy.RequireRole(RoleNames.ElectoralOfficer));
    options.AddPolicy("VerifyResults", policy => policy.RequireRole(RoleNames.Observer));
    options.AddPolicy("PublishResults", policy => policy.RequireRole(RoleNames.SuperAdministrator));
    options.AddPolicy("ManageUsers", policy => policy.RequireRole(RoleNames.SuperAdministrator));
    options.AddPolicy("ManageSettings", policy => policy.RequireRole(RoleNames.SuperAdministrator));
    options.AddPolicy("ViewAudit", policy => policy.RequireRole(RoleNames.SuperAdministrator, RoleNames.Observer));
    options.AddPolicy("AdminDashboard", policy =>
        policy.RequireRole(RoleNames.SuperAdministrator, RoleNames.ElectoralOfficer));
    options.AddPolicy("ObserverPortal", policy => policy.RequireRole(RoleNames.Observer));
    options.AddPolicy("ManageAnnouncements", policy =>
        policy.RequireRole(RoleNames.SuperAdministrator, RoleNames.ElectoralOfficer));
    options.AddPolicy("ViewUnpublishedResults", policy =>
        policy.RequireRole(RoleNames.SuperAdministrator, RoleNames.ElectoralOfficer, RoleNames.Observer));
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "SecureVote.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
    {
        var limit = builder.Configuration.GetValue("RateLimit:LoginPermitLimit", 10);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
    options.AddPolicy("ballot", context =>
    {
        var limit = builder.Configuration.GetValue("RateLimit:BallotPermitLimit", 10);
        var identity = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            identity,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddOpenApi();

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath))
{
    keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
}

Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("SecureVote")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

var app = builder.Build();

if (app.Configuration.GetValue("Security:UseForwardedHeaders", false))
{
    var forwardedHeaders = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                           ForwardedHeaders.XForwardedProto |
                           ForwardedHeaders.XForwardedHost
    };
    forwardedHeaders.KnownIPNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeaders);
}
if (!app.Environment.IsDevelopment() && !isTesting)
{
    app.UseHsts();
}

if (app.Configuration.GetValue("Security:UseHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();


app.UseMiddleware<SecurityHeadersMiddleware>();
if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();

if (File.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html")))
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

var seedOptions = app.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
SeedPolicy.EnsureAllowed(seedOptions.Enabled, app.Environment.EnvironmentName);

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().InitializeAsync();
}

await app.RunAsync();

public partial class Program;
