using System.Globalization;
using System.Net.Mail;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureVoting.Application.Contracts;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Students;

public sealed class StudentImportService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<StudentImportService> logger,
    DepartmentAccessService access)
{
    private const int MaximumRows = 5000;

    public async Task<VoterImportResponse> ImportAsync(
        Guid electionId,
        Stream csvStream,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var election = await db.Elections
            .Include(e => e.DepartmentRef)
            .SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        await access.EnsureCanManageElectionAsync(actorUserId, electionId, cancellationToken);
        if (election.Status is not (ElectionStatus.Draft or ElectionStatus.Scheduled))
        {
            throw new ConflictException("Eligibility can only be imported before voting opens.");
        }

        if (!election.DepartmentRef.IsActive)
        {
            throw new ConflictException("The election department is inactive.");
        }

        List<StudentCsvRow> rows;
        try
        {
            using var reader = new StreamReader(csvStream, leaveOpen: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
            });
            csv.Context.RegisterClassMap<StudentCsvRowMap>();
            rows = csv.GetRecords<StudentCsvRow>().ToList();
        }
        catch (CsvHelperException ex)
        {
            return new VoterImportResponse
            {
                Errors = [new VoterImportIssue(0, $"CSV could not be read: {ex.Message}")]
            };
        }

        if (rows.Count > MaximumRows)
        {
            return new VoterImportResponse
            {
                Errors = [new VoterImportIssue(0, $"The import contains more than {MaximumRows} rows.")]
            };
        }

        if (rows.Count == 0)
        {
            return new VoterImportResponse
            {
                Errors = [new VoterImportIssue(0, "The import is empty.")]
            };
        }

        var errors = new List<VoterImportIssue>();
        var normalizedRows = new List<ValidatedImportRow>();
        var seenMatric = new HashSet<string>(StringComparer.Ordinal);
        var seenEmail = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowNumber = index + 2;
            if (!MatriculationNumber.TryNormalize(row.MatricNumber, out var matric))
            {
                errors.Add(new VoterImportIssue(rowNumber, "Matriculation number is invalid."));
                continue;
            }

            var fullName = row.FullName?.Trim() ?? string.Empty;
            if (fullName.Length < 2 || fullName.Length > 160)
            {
                errors.Add(new VoterImportIssue(rowNumber, "Full name must be 2-160 characters."));
                continue;
            }

            var email = row.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!MailAddress.TryCreate(email, out _) || email.Length > 254)
            {
                errors.Add(new VoterImportIssue(rowNumber, "Email address is invalid."));
                continue;
            }

            if (!int.TryParse(row.Level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ||
                level is < 100 or > 900)
            {
                errors.Add(new VoterImportIssue(rowNumber, "Level must be a number between 100 and 900."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Department) &&
                !string.Equals(row.Department.Trim(), election.DepartmentRef.Name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.Department.Trim(), election.DepartmentRef.Code, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new VoterImportIssue(rowNumber, "Department does not match the election department."));
                continue;
            }

            if (!seenMatric.Add(matric))
            {
                errors.Add(new VoterImportIssue(rowNumber, "Duplicate matriculation number in this file."));
                continue;
            }

            if (!seenEmail.Add(email))
            {
                errors.Add(new VoterImportIssue(rowNumber, "Duplicate email address in this file."));
                continue;
            }

            normalizedRows.Add(new ValidatedImportRow(rowNumber, matric, fullName, email, level));
        }

        var existingStudents = await db.Students
            .Where(s => normalizedRows.Select(r => r.Matric).Contains(s.MatricNumberNormalized) ||
                        normalizedRows.Select(r => r.Email).Contains(s.Email))
            .ToListAsync(cancellationToken);
        foreach (var row in normalizedRows)
        {
            var byMatric = existingStudents.SingleOrDefault(s => s.MatricNumberNormalized == row.Matric);
            var byEmail = existingStudents.SingleOrDefault(s => s.Email == row.Email);
            if (byEmail is not null && byEmail.Id != byMatric?.Id)
            {
                errors.Add(new VoterImportIssue(row.RowNumber, "Email address belongs to another student record."));
            }

            if (byMatric is not null && byMatric.DepartmentId != election.DepartmentId)
            {
                errors.Add(new VoterImportIssue(row.RowNumber, "Matriculation number belongs to another department."));
            }
        }

        if (errors.Count > 0)
        {
            return new VoterImportResponse { Errors = errors };
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activationTokens = new List<ActivationCredentialDto>();
        var created = 0;
        var updated = 0;
        var accountsCreated = 0;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in normalizedRows)
            {
                var student = existingStudents.SingleOrDefault(s => s.MatricNumberNormalized == row.Matric);
                if (student is null)
                {
                    student = new Student
                    {
                        MatricNumber = row.Matric,
                        MatricNumberNormalized = row.Matric,
                        FullName = row.FullName,
                        Email = row.Email,
                        Department = election.DepartmentRef.Name,
                        DepartmentId = election.DepartmentId,
                        Level = row.Level,
                        IsActive = true,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                    db.Students.Add(student);
                    created++;
                }
                else
                {
                    student.FullName = row.FullName;
                    student.Email = row.Email;
                    student.Department = election.DepartmentRef.Name;
                    student.DepartmentId = election.DepartmentId;
                    student.Level = row.Level;
                    student.UpdatedAtUtc = now;
                    updated++;
                }

                var user = student.UserId is not null
                    ? await userManager.FindByIdAsync(student.UserId.Value.ToString())
                    : await userManager.FindByNameAsync(row.Matric);
                user ??= await userManager.FindByEmailAsync(row.Email);

                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = row.Matric,
                        Email = row.Email,
                        EmailConfirmed = false,
                        DisplayName = row.FullName,
                        IsActive = true,
                        CreatedAtUtc = now
                    };
                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        throw new ConflictException(
                            $"Could not create the student account for {row.Matric}: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
                    }

                    accountsCreated++;
                }
                else if (student.UserId is null)
                {
                    if (!string.IsNullOrWhiteSpace(user.Email) &&
                        !string.Equals(user.Email, row.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ConflictException($"The account for {row.Matric} uses a different email address.");
                    }

                    if (await db.Students.AnyAsync(s => s.UserId == user.Id && s.Id != student.Id, cancellationToken))
                    {
                        throw new ConflictException($"The account for {row.Matric} is already linked to another student.");
                    }
                }

                if (!await userManager.IsInRoleAsync(user, RoleNames.Student))
                {
                    await userManager.AddToRoleAsync(user, RoleNames.Student);
                }

                student.UserId = user.Id;

                if (!await userManager.HasPasswordAsync(user))
                {
                    var token = TokenGenerator.Create();
                    var expiresAt = now.AddDays(7);
                    var previousTokens = await db.StudentActivationTokens
                        .Where(t => t.StudentId == student.Id && t.UsedAtUtc == null)
                        .ToListAsync(cancellationToken);
                    foreach (var previous in previousTokens)
                    {
                        previous.UsedAtUtc = now;
                    }

                    db.StudentActivationTokens.Add(new StudentActivationToken
                    {
                        StudentId = student.Id,
                        TokenHash = TokenGenerator.Sha256Hex(token),
                        ExpiresAtUtc = expiresAt,
                        CreatedByUserId = actorUserId,
                        CreatedAtUtc = now
                    });
                    activationTokens.Add(new ActivationCredentialDto(row.Matric, token, expiresAt));
                }

                var eligibility = await db.VoterEligibility
                    .SingleOrDefaultAsync(x => x.ElectionId == electionId && x.StudentId == student.Id, cancellationToken);
                if (eligibility is null)
                {
                    eligibility = new VoterEligibility
                    {
                        ElectionId = electionId,
                        StudentId = student.Id,
                        IsEligible = true,
                        ImportedAtUtc = now,
                        Source = "CSV import"
                    };
                    db.VoterEligibility.Add(eligibility);
                }
                else
                {
                    eligibility.IsEligible = true;
                    eligibility.ImportedAtUtc = now;
                    eligibility.Source = "CSV import";
                }

                if (!await db.VoterEntitlements.AnyAsync(
                        x => x.ElectionId == electionId && x.StudentId == student.Id,
                        cancellationToken))
                {
                    db.VoterEntitlements.Add(new VoterEntitlement
                    {
                        ElectionId = electionId,
                        StudentId = student.Id,
                        VoterEligibility = eligibility,
                        CreatedAtUtc = now
                    });
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "VoterRegisterImported",
                nameof(Election),
                electionId.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                correlationId,
                new
                {
                    rows = normalizedRows.Count,
                    created,
                    updated,
                    accountsCreated,
                    duplicates = rows.Count - normalizedRows.Count,
                    departmentId = election.DepartmentId
                },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(ex, "Voter import rolled back for election {ElectionId}.", electionId);
            throw;
        }

        logger.LogInformation(
            "Voter import completed for election {ElectionId}: {Rows} rows, {Accounts} new accounts.",
            electionId,
            normalizedRows.Count,
            accountsCreated);
        return new VoterImportResponse
        {
            RowsProcessed = normalizedRows.Count,
            EligibleCreated = created,
            EligibilityUpdated = updated,
            AccountsCreated = accountsCreated,
            DuplicateRows = rows.Count - normalizedRows.Count,
            ActivationTokens = activationTokens
        };
    }

    private sealed class StudentCsvRow
    {
        public string? MatricNumber { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Level { get; set; }
        public string? Department { get; set; }
    }

    private sealed class StudentCsvRowMap : ClassMap<StudentCsvRow>
    {
        public StudentCsvRowMap()
        {
            Map(x => x.MatricNumber).Name("matric", "matricnumber", "matric_number");
            Map(x => x.FullName).Name("fullname", "full_name", "name");
            Map(x => x.Email).Name("email", "emailaddress", "email_address");
            Map(x => x.Level).Name("level");
            Map(x => x.Department).Name("department");
        }
    }

    private sealed record ValidatedImportRow(
        int RowNumber,
        string Matric,
        string FullName,
        string Email,
        int Level);
}
