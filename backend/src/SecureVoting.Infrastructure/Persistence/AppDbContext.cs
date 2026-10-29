using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Identity;

namespace SecureVoting.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentAssignment> DepartmentAssignments => Set<DepartmentAssignment>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentActivationToken> StudentActivationTokens => Set<StudentActivationToken>();
    public DbSet<Election> Elections => Set<Election>();
    public DbSet<ElectionRule> ElectionRules => Set<ElectionRule>();
    public DbSet<ElectionPosition> ElectionPositions => Set<ElectionPosition>();
    public DbSet<VoterEligibility> VoterEligibility => Set<VoterEligibility>();
    public DbSet<VoterEntitlement> VoterEntitlements => Set<VoterEntitlement>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Ballot> Ballots => Set<Ballot>();
    public DbSet<BallotSelection> BallotSelections => Set<BallotSelection>();
    public DbSet<BallotAbstention> BallotAbstentions => Set<BallotAbstention>();
    public DbSet<BallotReceipt> BallotReceipts => Set<BallotReceipt>();
    public DbSet<ElectionResult> ElectionResults => Set<ElectionResult>();
    public DbSet<ElectionResultLine> ElectionResultLines => Set<ElectionResultLine>();
    public DbSet<ResultApproval> ResultApprovals => Set<ResultApproval>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuditChainState> AuditChainStates => Set<AuditChainState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");
        });

        builder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CodeNormalized).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Faculty).HasMaxLength(160);
            entity.HasIndex(x => x.CodeNormalized).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        builder.Entity<DepartmentAssignment>(entity =>
        {
            entity.ToTable("DepartmentAssignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => new { x.UserId, x.DepartmentId, x.Scope }).IsUnique();
            entity.HasIndex(x => x.DepartmentId);
            entity.HasOne(x => x.Department).WithMany(x => x.Assignments)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MatricNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.MatricNumberNormalized).HasMaxLength(40).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.Department).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.MatricNumberNormalized).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.DepartmentId);
            entity.HasOne(x => x.DepartmentRef).WithMany(x => x.Students)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StudentActivationToken>(entity =>
        {
            entity.ToTable("StudentActivationTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.Student).WithMany(x => x.ActivationTokens)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Election>(entity =>
        {
            entity.ToTable("Elections", table =>
            {
                table.HasCheckConstraint("CK_Elections_Window", "\"ClosesAtUtc\" > \"OpensAtUtc\"");
                table.HasCheckConstraint("CK_Elections_NominationWindow",
                    "\"NominationClosesAtUtc\" > \"NominationOpensAtUtc\" AND \"NominationClosesAtUtc\" <= \"OpensAtUtc\"");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.AcademicSession).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Department).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.SuspendedFromStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.SuspensionReason).HasMaxLength(500);
            entity.Property(x => x.ClosureReason).HasMaxLength(500);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.OpensAtUtc);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasOne(x => x.DepartmentRef).WithMany(x => x.Elections)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Version).IsRowVersion();
        });

        builder.Entity<ElectionRule>(entity =>
        {
            entity.ToTable("ElectionRules");
            entity.HasKey(x => x.ElectionId);
            entity.Property(x => x.TieHandling).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(x => x.Election).WithOne(x => x.Rule)
                .HasForeignKey<ElectionRule>(x => x.ElectionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ElectionPosition>(entity =>
        {
            entity.ToTable("ElectionPositions", table =>
            {
                table.HasCheckConstraint("CK_ElectionPositions_Seats", "\"SeatCount\" >= 1");
                table.HasCheckConstraint("CK_ElectionPositions_Selections",
                    "\"MaxSelections\" >= 1 AND \"MaxSelections\" <= \"SeatCount\"");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.ElectionId, x.Title }).IsUnique();
            entity.HasOne(x => x.Election).WithMany(x => x.Positions)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VoterEligibility>(entity =>
        {
            entity.ToTable("VoterEligibility");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Source).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.ElectionId, x.StudentId }).IsUnique();
            entity.HasOne(x => x.Election).WithMany(x => x.VoterEligibility)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Student).WithMany(x => x.EligibilityRecords)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VoterEntitlement>(entity =>
        {
            entity.ToTable("VoterEntitlements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConfirmationCodeHash).HasMaxLength(64);
            entity.HasIndex(x => new { x.ElectionId, x.StudentId }).IsUnique();
            entity.HasIndex(x => x.VoterEligibilityId).IsUnique();
            entity.HasOne(x => x.Election).WithMany(x => x.VoterEntitlements)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VoterEligibility).WithOne(x => x.Entitlement)
                .HasForeignKey<VoterEntitlement>(x => x.VoterEligibilityId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Candidate>(entity =>
        {
            entity.ToTable("Candidates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Manifesto).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.PhotoFileName).HasMaxLength(120);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.WithdrawalReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.ElectionId, x.StudentId }).IsUnique();
            entity.HasIndex(x => new { x.ElectionId, x.PositionId, x.Status });
            entity.HasOne(x => x.Election).WithMany(x => x.Candidates)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Position).WithMany(x => x.Candidates)
                .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Student).WithMany(x => x.Candidates)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Ballot>(entity =>
        {
            entity.ToTable("Ballots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.ElectionId, x.SubmittedAtUtc });
            entity.HasOne(x => x.Election).WithMany(x => x.Ballots)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BallotSelection>(entity =>
        {
            entity.ToTable("BallotSelections");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BallotId, x.ElectionPositionId, x.CandidateId }).IsUnique();
            entity.HasIndex(x => new { x.ElectionPositionId, x.CandidateId });
            entity.HasOne(x => x.Ballot).WithMany(x => x.Selections)
                .HasForeignKey(x => x.BallotId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ElectionPosition).WithMany(x => x.BallotSelections)
                .HasForeignKey(x => x.ElectionPositionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Candidate).WithMany(x => x.BallotSelections)
                .HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BallotAbstention>(entity =>
        {
            entity.ToTable("BallotAbstentions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BallotId, x.ElectionPositionId }).IsUnique();
            entity.HasOne(x => x.Ballot).WithMany(x => x.Abstentions)
                .HasForeignKey(x => x.BallotId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ElectionPosition).WithMany(x => x.BallotAbstentions)
                .HasForeignKey(x => x.ElectionPositionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BallotReceipt>(entity =>
        {
            entity.ToTable("BallotReceipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReceiptHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.BallotId).IsUnique();
            entity.HasIndex(x => x.ReceiptHash).IsUnique();
            entity.HasOne(x => x.Ballot).WithOne(x => x.Receipt)
                .HasForeignKey<BallotReceipt>(x => x.BallotId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ElectionResult>(entity =>
        {
            entity.ToTable("ElectionResults");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.IntegrityHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ChangeReason).HasMaxLength(1000);
            entity.Property(x => x.TurnoutPercentage).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.ElectionId, x.Version }).IsUnique();
            entity.HasIndex(x => x.ElectionId).IsUnique().HasFilter("\"Status\" = 'Published'");
            entity.HasOne(x => x.Election).WithMany(x => x.Results)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ElectionResultLine>(entity =>
        {
            entity.ToTable("ElectionResultLines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Percentage).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.ElectionResultId, x.PositionId, x.CandidateId }).IsUnique();
            entity.HasOne(x => x.ElectionResult).WithMany(x => x.Lines)
                .HasForeignKey(x => x.ElectionResultId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResultApproval>(entity =>
        {
            entity.ToTable("ResultApprovals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ApprovalType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ElectionResultId, x.UserId, x.ApprovalType }).IsUnique();
            entity.HasOne(x => x.ElectionResult).WithMany(x => x.Approvals)
                .HasForeignKey(x => x.ElectionResultId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Audience).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => x.PublishedAtUtc);
            entity.HasOne(x => x.Election).WithMany(x => x.Announcements)
                .HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ErrorSummary).HasMaxLength(500);
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Election>().WithMany().HasForeignKey(x => x.ElectionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorDisplay).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => x.DepartmentId);
            entity.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.PreviousHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Hash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Sequence).IsUnique();
            entity.HasIndex(x => x.EventId).IsUnique();
            entity.HasIndex(x => new { x.OccurredAtUtc, x.EventType });
            entity.HasIndex(x => x.ActorUserId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditChainState>(entity =>
        {
            entity.ToTable("AuditChainState");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LastHash).HasMaxLength(64).IsRequired();
            entity.HasData(new AuditChainState
            {
                Id = 1,
                LastSequence = 0,
                LastHash = new string('0', 64),
                UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp with time zone");
                }
            }
        }
    }
}
