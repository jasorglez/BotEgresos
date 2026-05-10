using EgresosBot.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EgresosBot.Api.Infrastructure.Persistence;

public sealed class EgresosBotDbContext(DbContextOptions<EgresosBotDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BotLink> BotLinks => Set<BotLink>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<BotSession> BotSessions => Set<BotSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Slug).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(180);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.FirstName).HasMaxLength(120);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.PasswordAlgorithm).HasMaxLength(30);
        });

        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.ToTable("TenantUsers");
            entity.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(30);
            entity.Property(x => x.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Subscription>().ToTable("Subscriptions");
        modelBuilder.Entity<Payment>().ToTable("Payments");
        modelBuilder.Entity<RefreshToken>().ToTable("RefreshTokens");

        modelBuilder.Entity<BotLink>(entity =>
        {
            entity.ToTable("BotLinks");
            entity.HasIndex(x => x.ExternalChatId);
            entity.HasIndex(x => x.PhoneNumber);
            entity.Property(x => x.Channel).HasMaxLength(20);
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.ToTable("ExpenseCategories");
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Payee>().ToTable("Payees");

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasOne(x => x.CreatedByUser)
                .WithMany(x => x.CreatedExpenses)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseAttachment>().ToTable("ExpenseAttachments");

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("ApprovalRequests");
            entity.HasOne(x => x.RequestedByUser)
                .WithMany(x => x.ApprovalRequestsCreated)
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApproverUser)
                .WithMany(x => x.ApprovalRequestsToApprove)
                .HasForeignKey(x => x.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BotSession>(entity =>
        {
            entity.ToTable("BotSessions");
            entity.HasIndex(x => new { x.Channel, x.ChatId }).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasOne(x => x.User)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
