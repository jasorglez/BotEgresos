namespace EgresosBot.Api.Domain.Entities;

public sealed class Tenant
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string CountryCode { get; set; } = "MX";
    public string TimeZone { get; set; } = "America/Mexico_City";
    public string CurrencyCode { get; set; } = "MXN";
    public string Status { get; set; } = "ACTIVE";
    public DateTime? TrialEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<BotLink> BotLinks { get; set; } = new List<BotLink>();
    public ICollection<ExpenseCategory> ExpenseCategories { get; set; } = new List<ExpenseCategory>();
    public ICollection<Payee> Payees { get; set; } = new List<Payee>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<BotSession> BotSessions { get; set; } = new List<BotSession>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public sealed class AppUser
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string PasswordAlgorithm { get; set; } = "ASPNET_IDENTITY_V3";
    public bool EmailConfirmed { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<BotLink> BotLinks { get; set; } = new List<BotLink>();
    public ICollection<Expense> CreatedExpenses { get; set; } = new List<Expense>();
    public ICollection<ApprovalRequest> ApprovalRequestsCreated { get; set; } = new List<ApprovalRequest>();
    public ICollection<ApprovalRequest> ApprovalRequestsToApprove { get; set; } = new List<ApprovalRequest>();
    public ICollection<BotSession> BotSessions { get; set; } = new List<BotSession>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public sealed class TenantUser
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string RoleCode { get; set; } = null!;
    public bool IsOwner { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public sealed class SubscriptionPlan
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal PriceMonthly { get; set; }
    public string CurrencyCode { get; set; } = "MXN";
    public int MaxUsers { get; set; }
    public int MaxBranches { get; set; }
    public bool HasApprovals { get; set; }
    public bool HasAdvancedReports { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

public sealed class Subscription
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int PlanId { get; set; }
    public string Provider { get; set; } = null!;
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
    public string Status { get; set; } = "TRIALING";
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public sealed class Payment
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public string Provider { get; set; } = null!;
    public string? ProviderPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "MXN";
    public string Status { get; set; } = null!;
    public DateTime? PaidAt { get; set; }
    public string? RawJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public Subscription Subscription { get; set; } = null!;
}

public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TenantId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}

public sealed class BotLink
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = null!;
    public string? ExternalUserId { get; set; }
    public string? ExternalChatId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
    public string? LinkCode { get; set; }
    public DateTime? LinkedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public sealed class ExpenseCategory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public sealed class Payee
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string TypeCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public sealed class Expense
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int TenantId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? CategoryId { get; set; }
    public int? PayeeId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateOnly ExpenseDate { get; set; }
    public string Description { get; set; } = null!;
    public string? Notes { get; set; }
    public decimal AmountSubtotal { get; set; }
    public decimal IvaAmount { get; set; }
    public decimal AmountTotal { get; set; }
    public string CurrencyCode { get; set; } = "MXN";
    public string? PaymentMethod { get; set; }
    public string? BranchName { get; set; }
    public string? CostCenter { get; set; }
    public string? ProjectName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? AccountedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
    public ExpenseCategory? Category { get; set; }
    public Payee? Payee { get; set; }
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();
    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();
}

public sealed class ExpenseAttachment
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string StorageProvider { get; set; } = null!;
    public string StorageUrl { get; set; } = null!;
    public string? OcrText { get; set; }
    public DateTime UploadedAt { get; set; }

    public Expense Expense { get; set; } = null!;
}

public sealed class ApprovalRequest
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int RequestedByUserId { get; set; }
    public int ApproverUserId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Comments { get; set; }

    public Expense Expense { get; set; } = null!;
    public AppUser RequestedByUser { get; set; } = null!;
    public AppUser ApproverUser { get; set; } = null!;
}

public sealed class BotSession
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = null!;
    public string ChatId { get; set; } = null!;
    public string CurrentState { get; set; } = null!;
    public string? SessionJson { get; set; }
    public DateTime LastInteractionAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public int? TenantId { get; set; }
    public int? UserId { get; set; }
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string ActionCode { get; set; } = null!;
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public AppUser? User { get; set; }
}
