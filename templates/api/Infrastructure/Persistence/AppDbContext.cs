using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Code-first DbContext (DATABASE_CODE_FIRST.md): snake_case naming is
/// applied via UseSnakeCaseNamingConvention at registration; RLS and the
/// PostGIS extension ride inside migrations.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AppInstance> AppInstances => Set<AppInstance>();
    public DbSet<SignupVerification> SignupVerifications => Set<SignupVerification>();
    public DbSet<UserGoogleIdentity> UserGoogleIdentities => Set<UserGoogleIdentity>();
    public DbSet<UserFirebaseIdentity> UserFirebaseIdentities => Set<UserFirebaseIdentity>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<UserExport> UserExports => Set<UserExport>();
    public DbSet<UserTotals> UserTotals => Set<UserTotals>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrgUser> OrgUsers => Set<OrgUser>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MemberUser> MemberUsers => Set<MemberUser>();
    public DbSet<MemberInvitation> MemberInvitations => Set<MemberInvitation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<ProjectTotals> ProjectTotals => Set<ProjectTotals>();
    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
