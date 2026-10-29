using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Configuration;
using SecureVoting.Infrastructure.Identity;

namespace SecureVoting.Infrastructure.Persistence;

public sealed class DatabaseSeeder(
    AppDbContext db,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseSeeder> logger)
{
    public const string DemoElectionTitle = "2026/2027 Computer Science Departmental Election";
    public const string DemoDepartment = "Computer Science";
    public const string DemoDepartmentCode = "CSC";
    public const string DemoMathDepartment = "Mathematics";
    public const string DemoMathDepartmentCode = "MTH";
    public const string DemoPhysicsDepartment = "Physics";
    public const string DemoPhysicsDepartmentCode = "PHY";
    public const string DemoSession = "2026/2027";

    private static readonly (string FullName, string Matric, int Level)[] DemoStudents =
    [
        ("Adaeze N. Okafor", "IAUE/CS/22/001", 300),
        ("Chinedu M. Eze", "IAUE/CS/22/002", 300),
        ("Fatima A. Bello", "IAUE/CS/22/003", 300),
        ("Ifeanyi K. Nwosu", "IAUE/CS/22/004", 300),
        ("Blessing T. Adeyemi", "IAUE/CS/22/005", 300),
        ("Emeka P. Obi", "IAUE/CS/22/006", 400),
        ("Zainab O. Sule", "IAUE/CS/22/007", 400),
        ("Tunde A. Balogun", "IAUE/CS/22/008", 400),
        ("Ngozi C. Uche", "IAUE/CS/22/009", 400),
        ("Samuel I. Etim", "IAUE/CS/22/010", 400),
        ("Grace E. Okon", "IAUE/CS/22/011", 400),
        ("Peter U. Amadi", "IAUE/CS/22/012", 400)
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await EnsureRolesAsync();
        var departments = await EnsureDepartmentsAsync(cancellationToken);
        await EnsureSettingsAsync(cancellationToken);

        var options = seedOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Demo data seeding is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.DemoPassword) || options.DemoPassword.Length < 12)
        {
            throw new InvalidOperationException(
                "Seed:DemoPassword must be configured with at least 12 characters when demo seeding is enabled.");
        }

        await EnsureDemoUsersAsync(options.DemoPassword, departments);
        await EnsureDemoElectionAsync(departments);
        await EnsureAdditionalDemoElectionsAsync(departments);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Demo election data is available.");
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create role {role}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }

    private async Task EnsureSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new Dictionary<string, string>
        {
            ["ApplicationName"] = "IAUE SecureVote",
            ["InstitutionName"] = "Ignatius Ajuru University of Education",
            ["DemoMode"] = seedOptions.Value.Enabled ? "true" : "false"
        };
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in defaults)
        {
            if (!await db.SystemSettings.AnyAsync(x => x.Key == item.Key, cancellationToken))
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = item.Key,
                    Value = item.Value,
                    UpdatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, Department>> EnsureDepartmentsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        var specifications = new[]
        {
            (Name: DemoDepartment, Code: DemoDepartmentCode),
            (Name: DemoMathDepartment, Code: DemoMathDepartmentCode),
            (Name: DemoPhysicsDepartment, Code: DemoPhysicsDepartmentCode)
        };
        foreach (var specification in specifications)
        {
            var code = specification.Code.ToUpperInvariant();
            var department = await db.Departments.SingleOrDefaultAsync(
                                 d => d.CodeNormalized == code, cancellationToken)
                             ?? await db.Departments.SingleOrDefaultAsync(
                                 d => d.Name.ToLower() == specification.Name.ToLower(), cancellationToken);
            if (department is null)
            {
                department = new Department
                {
                    Name = specification.Name,
                    Code = code,
                    CodeNormalized = code,
                    Faculty = "Faculty of Natural and Applied Sciences",
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.Departments.Add(department);
            }
            else
            {
                department.Name = specification.Name;
                department.Code = code;
                department.CodeNormalized = code;
                department.Faculty ??= "Faculty of Natural and Applied Sciences";
                department.UpdatedAtUtc = now;
            }

            result[code] = department;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task EnsureDemoUsersAsync(
        string password,
        IReadOnlyDictionary<string, Department> departments)
    {
        await EnsureStaffUserAsync(
            "superadmin@demo.iaue.edu.ng",
            "Demo Super Administrator",
            RoleNames.SuperAdministrator,
            password);
        var officer = await EnsureStaffUserAsync(
            "officer@demo.iaue.edu.ng",
            "Demo Electoral Officer",
            RoleNames.ElectoralOfficer,
            password);
        var auditor = await EnsureStaffUserAsync(
            "auditor@demo.iaue.edu.ng",
            "Demo Independent Observer",
            RoleNames.Observer,
            password);
        await EnsureAssignmentAsync(
            officer.Id, departments[DemoDepartmentCode].Id, DepartmentAccessScope.ElectoralOfficer);
        await EnsureAssignmentAsync(
            auditor.Id, departments[DemoDepartmentCode].Id, DepartmentAccessScope.Observer);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var (fullName, matric, level) in DemoStudents)
        {
            var normalizedMatric = MatriculationNumber.Normalize(matric);
            var email = $"{matric.Replace("/", string.Empty).ToLowerInvariant()}@demo.iaue.edu.ng";
            var user = await userManager.FindByNameAsync(normalizedMatric);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = normalizedMatric,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = fullName,
                    IsActive = true,
                    CreatedAtUtc = now
                };
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create demo student {normalizedMatric}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, RoleNames.Student))
            {
                await userManager.AddToRoleAsync(user, RoleNames.Student);
            }

            var student = await db.Students.SingleOrDefaultAsync(x => x.MatricNumberNormalized == normalizedMatric);
            if (student is null)
            {
                student = new Student
                {
                    MatricNumber = normalizedMatric,
                    MatricNumberNormalized = normalizedMatric,
                    FullName = fullName,
                    Email = email,
                    Department = departments[DemoDepartmentCode].Name,
                    DepartmentId = departments[DemoDepartmentCode].Id,
                    Level = level,
                    IsActive = true,
                    UserId = user.Id,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.Students.Add(student);
            }
            else if (student.UserId is null)
            {
                student.UserId = user.Id;
            }
        }
    }

    private async Task EnsureAdditionalDemoElectionsAsync(IReadOnlyDictionary<string, Department> departments)
    {
        var mathStudents = Enumerable.Range(1, 6)
            .Select(index => ($"Mathematics Demo Student {index}", $"IAUE/MTH/22/{index:000}"))
            .ToArray();
        var physicsStudents = Enumerable.Range(1, 6)
            .Select(index => ($"Physics Demo Student {index}", $"IAUE/PHY/22/{index:000}"))
            .ToArray();
        await EnsureSampleDepartmentElectionAsync(
            departments[DemoMathDepartmentCode], mathStudents, "officer.math@demo.iaue.edu.ng",
            "observer.math@demo.iaue.edu.ng");
        await EnsureSampleDepartmentElectionAsync(
            departments[DemoPhysicsDepartmentCode], physicsStudents, "officer.physics@demo.iaue.edu.ng",
            "observer.physics@demo.iaue.edu.ng");
    }

    private async Task EnsureSampleDepartmentElectionAsync(
        Department department,
        IReadOnlyList<(string FullName, string Matric)> sampleStudents,
        string officerEmail,
        string observerEmail)
    {
        var password = seedOptions.Value.DemoPassword;
        var officer = await EnsureStaffUserAsync(
            officerEmail, $"{department.Name} Demo Electoral Officer", RoleNames.ElectoralOfficer, password);
        var observer = await EnsureStaffUserAsync(
            observerEmail, $"{department.Name} Demo Observer", RoleNames.Observer, password);
        await EnsureAssignmentAsync(officer.Id, department.Id, DepartmentAccessScope.ElectoralOfficer);
        await EnsureAssignmentAsync(observer.Id, department.Id, DepartmentAccessScope.Observer);

        var students = new List<Student>();
        foreach (var (fullName, matric) in sampleStudents)
        {
            var normalizedMatric = MatriculationNumber.Normalize(matric);
            var email = $"{matric.Replace("/", string.Empty).ToLowerInvariant()}@demo.iaue.edu.ng";
            var user = await userManager.FindByNameAsync(normalizedMatric);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = normalizedMatric,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = fullName,
                    IsActive = true,
                    CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
                };
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create sample student {normalizedMatric}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, RoleNames.Student))
            {
                await userManager.AddToRoleAsync(user, RoleNames.Student);
            }

            var student = await db.Students.SingleOrDefaultAsync(s => s.MatricNumberNormalized == normalizedMatric);
            if (student is null)
            {
                student = new Student
                {
                    MatricNumber = normalizedMatric,
                    MatricNumberNormalized = normalizedMatric,
                    FullName = fullName,
                    Email = email,
                    Department = department.Name,
                    DepartmentId = department.Id,
                    Level = 300,
                    IsActive = true,
                    UserId = user.Id,
                    CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
                };
                db.Students.Add(student);
            }

            students.Add(student);
        }

        await db.SaveChangesAsync();
        var title = $"{DemoSession} {department.Name} Departmental Election";
        if (await db.Elections.AnyAsync(e => e.DepartmentId == department.Id && e.Title == title))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var election = new Election
        {
            Title = title,
            Description = $"Synthetic {department.Name} departmental election for multi-department demonstration. No real student or ballot data is used.",
            AcademicSession = DemoSession,
            DepartmentId = department.Id,
            Department = department.Name,
            Status = ElectionStatus.Draft,
            NominationOpensAtUtc = now.AddDays(-7),
            NominationClosesAtUtc = now.AddDays(-1),
            OpensAtUtc = now.AddHours(2),
            ClosesAtUtc = now.AddHours(8),
            CreatedByUserId = officer.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Rule = new ElectionRule { TieHandling = TieHandling.RunoffRequired, AllowAbstention = true, NominationRequiresApproval = true }
        };
        db.Elections.Add(election);
        var positions = new[]
        {
            new ElectionPosition { Election = election, Title = "President", Description = "Leads the department student body.", DisplayOrder = 1, SeatCount = 1, MaxSelections = 1, CreatedAtUtc = now, UpdatedAtUtc = now },
            new ElectionPosition { Election = election, Title = "General Secretary", Description = "Coordinates records and communication.", DisplayOrder = 2, SeatCount = 1, MaxSelections = 1, CreatedAtUtc = now, UpdatedAtUtc = now }
        };
        db.ElectionPositions.AddRange(positions);
        foreach (var student in students)
        {
            var eligibility = new VoterEligibility
            {
                Election = election,
                Student = student,
                IsEligible = true,
                ImportedAtUtc = now,
                Source = "Demo seed"
            };
            db.VoterEligibility.Add(eligibility);
            db.VoterEntitlements.Add(new VoterEntitlement
            {
                Election = election,
                Student = student,
                VoterEligibility = eligibility,
                CreatedAtUtc = now
            });
        }

        for (var index = 0; index < 4; index++)
        {
            var student = students[index];
            var position = index < 2 ? positions[0] : positions[1];
            db.Candidates.Add(new Candidate
            {
                Election = election,
                Position = position,
                Student = student,
                DisplayName = student.FullName,
                Manifesto = "Synthetic demonstration manifesto for a multi-department election.",
                Status = CandidateStatus.Approved,
                SubmittedAtUtc = now.AddDays(-2),
                ReviewedAtUtc = now.AddDays(-1),
                ReviewedByUserId = officer.Id,
                CreatedAtUtc = now.AddDays(-2),
                UpdatedAtUtc = now.AddDays(-1)
            });
        }

        await db.SaveChangesAsync();
        foreach (var student in students.Take(4))
        {
            var user = await userManager.FindByIdAsync(student.UserId!.Value.ToString());
            if (user is not null && !await userManager.IsInRoleAsync(user, RoleNames.Candidate))
            {
                await userManager.AddToRoleAsync(user, RoleNames.Candidate);
            }
        }
    }

    private async Task<ApplicationUser> EnsureStaffUserAsync(string email, string displayName, string role, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo staff {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private async Task EnsureAssignmentAsync(Guid userId, Guid departmentId, DepartmentAccessScope scope)
    {
        if (await db.DepartmentAssignments.AnyAsync(
                a => a.UserId == userId && a.DepartmentId == departmentId && a.Scope == scope))
        {
            return;
        }

        db.DepartmentAssignments.Add(new DepartmentAssignment
        {
            UserId = userId,
            DepartmentId = departmentId,
            Scope = scope,
            CreatedByUserId = userId,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync();
    }

    private async Task EnsureDemoElectionAsync(IReadOnlyDictionary<string, Department> departments)
    {
        var department = departments[DemoDepartmentCode];
        var existing = await db.Elections
            .Include(e => e.Rule)
            .Include(e => e.Positions)
            .Include(e => e.Candidates)
            .SingleOrDefaultAsync(e => e.Title == DemoElectionTitle && e.AcademicSession == DemoSession);
        if (existing is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var officer = await userManager.FindByEmailAsync("officer@demo.iaue.edu.ng")
            ?? throw new InvalidOperationException("Demo officer account is missing.");
        var election = new Election
        {
            Title = DemoElectionTitle,
            Description = "A synthetic departmental election for demonstration, testing and defence. No real student or ballot data is used.",
            AcademicSession = DemoSession,
            DepartmentId = department.Id,
            Department = department.Name,
            Status = ElectionStatus.Draft,
            NominationOpensAtUtc = now.AddDays(-14),
            NominationClosesAtUtc = now.AddDays(-1),
            OpensAtUtc = now.AddMinutes(-5),
            ClosesAtUtc = now.AddHours(2),
            CreatedByUserId = officer.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Rule = new ElectionRule
            {
                TieHandling = TieHandling.RunoffRequired,
                AllowAbstention = true,
                NominationRequiresApproval = true
            }
        };
        db.Elections.Add(election);

        var positions = new[]
        {
            new ElectionPosition
            {
                Election = election,
                Title = "President",
                Description = "Leads the departmental student body for the academic session.",
                DisplayOrder = 1,
                SeatCount = 1,
                MaxSelections = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new ElectionPosition
            {
                Election = election,
                Title = "Vice President",
                Description = "Supports the President and coordinates departmental activities.",
                DisplayOrder = 2,
                SeatCount = 1,
                MaxSelections = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new ElectionPosition
            {
                Election = election,
                Title = "General Secretary",
                Description = "Maintains records and communicates decisions to students.",
                DisplayOrder = 3,
                SeatCount = 1,
                MaxSelections = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };
        db.ElectionPositions.AddRange(positions);

        var studentEntities = await db.Students
            .Where(s => s.DepartmentId == department.Id)
            .OrderBy(s => s.MatricNumberNormalized)
            .Take(DemoStudents.Length)
            .ToListAsync();
        foreach (var student in studentEntities)
        {
            var eligibility = new VoterEligibility
            {
                Election = election,
                Student = student,
                IsEligible = true,
                ImportedAtUtc = now,
                Source = "Demo seed"
            };
            db.VoterEligibility.Add(eligibility);
            db.VoterEntitlements.Add(new VoterEntitlement
            {
                Election = election,
                Student = student,
                VoterEligibility = eligibility,
                CreatedAtUtc = now
            });
        }

        var candidatePairs = new[]
        {
            (Position: positions[0], Student: studentEntities[1], Manifesto: "I will strengthen student representation and publish a monthly departmental update."),
            (Position: positions[0], Student: studentEntities[2], Manifesto: "I will focus on transparent budgeting, safer study spaces and inclusive decision-making."),
            (Position: positions[1], Student: studentEntities[3], Manifesto: "I will coordinate study support, tutorials and stronger communication with lecturers."),
            (Position: positions[1], Student: studentEntities[4], Manifesto: "I will build a reliable student feedback channel and support academic welfare."),
            (Position: positions[2], Student: studentEntities[5], Manifesto: "I will keep accurate records and ensure decisions are communicated promptly."),
            (Position: positions[2], Student: studentEntities[6], Manifesto: "I will promote clear documentation, accountability and an accessible student calendar.")
        };
        foreach (var (position, student, manifesto) in candidatePairs)
        {
            if (student.UserId is null)
            {
                continue;
            }

            db.Candidates.Add(new Candidate
            {
                Election = election,
                Position = position,
                Student = student,
                DisplayName = student.FullName,
                Manifesto = manifesto,
                Status = CandidateStatus.Approved,
                SubmittedAtUtc = now.AddDays(-2),
                ReviewedAtUtc = now.AddDays(-1),
                ReviewedByUserId = officer.Id,
                CreatedAtUtc = now.AddDays(-2),
                UpdatedAtUtc = now.AddDays(-1)
            });
        }

        db.Announcements.Add(new Announcement
        {
            Election = election,
            Title = "Departmental election demo is ready",
            Body = "This is synthetic demonstration data. The electoral officer can open the draft election, students can vote once, and the observer can verify results before publication.",
            Audience = AnnouncementAudience.Everyone,
            PublishedAtUtc = now,
            CreatedByUserId = officer.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync();

        foreach (var (_, student, _) in candidatePairs)
        {
            var user = await userManager.FindByIdAsync(student.UserId!.Value.ToString());
            if (user is not null && !await userManager.IsInRoleAsync(user, RoleNames.Candidate))
            {
                await userManager.AddToRoleAsync(user, RoleNames.Candidate);
            }
        }
    }
}
