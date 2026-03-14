using Microsoft.EntityFrameworkCore;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueAuditLog> IssueAuditLogs => Set<IssueAuditLog>();
    public DbSet<WorkPackage> WorkPackages => Set<WorkPackage>();
    public DbSet<WorkPackagePhase> WorkPackagePhases => Set<WorkPackagePhase>();
    public DbSet<WorkPackageTask> WorkPackageTasks => Set<WorkPackageTask>();
    public DbSet<AcceptanceCriterion> AcceptanceCriteria => Set<AcceptanceCriterion>();
    public DbSet<WorkPackageDependency> WorkPackageDependencies => Set<WorkPackageDependency>();
    public DbSet<WorkPackageTaskDependency> WorkPackageTaskDependencies => Set<WorkPackageTaskDependency>();
    public DbSet<WorkPackageAuditLog> WorkPackageAuditLogs => Set<WorkPackageAuditLog>();
    public DbSet<PhaseAuditLog> PhaseAuditLogs => Set<PhaseAuditLog>();
    public DbSet<TaskAuditLog> TaskAuditLogs => Set<TaskAuditLog>();
    public DbSet<FeatureRequest> FeatureRequests => Set<FeatureRequest>();
    public DbSet<FeatureRequestAuditLog> FeatureRequestAuditLogs => Set<FeatureRequestAuditLog>();
    public DbSet<WorkPackageIssueLink> WorkPackageIssueLinks => Set<WorkPackageIssueLink>();
    public DbSet<WorkPackageFeatureRequestLink> WorkPackageFeatureRequestLinks => Set<WorkPackageFeatureRequestLink>();
    public DbSet<ProjectMemory> ProjectMemories => Set<ProjectMemory>();
    public DbSet<ProjectMemoryAuditLog> ProjectMemoryAuditLogs => Set<ProjectMemoryAuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserProjectRole> UserProjectRoles => Set<UserProjectRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
