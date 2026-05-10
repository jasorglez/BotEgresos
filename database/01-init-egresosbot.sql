/*
    EgresosBot - SQL Server bootstrap
    Fecha: 2026-05-09
    Nota:
    - Este script crea la base, usuario y tablas base para el MVP SaaS.
    - Usa el login SQL Server existente: Microservicio
*/

IF DB_ID(N'EgresosBotDb') IS NULL
BEGIN
    CREATE DATABASE EgresosBotDb;
END
GO

USE EgresosBotDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'Microservicio')
BEGIN
    CREATE USER [Microservicio] FOR LOGIN [Microservicio];
END
GO

ALTER ROLE [db_datareader] ADD MEMBER [Microservicio];
GO

ALTER ROLE [db_datawriter] ADD MEMBER [Microservicio];
GO

ALTER ROLE [db_ddladmin] ADD MEMBER [Microservicio];
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'app')
BEGIN
    EXEC('CREATE SCHEMA app AUTHORIZATION dbo');
END
GO

IF OBJECT_ID(N'app.Tenants', N'U') IS NOT NULL DROP TABLE app.TenantUsers;
IF OBJECT_ID(N'app.BotLinks', N'U') IS NOT NULL DROP TABLE app.BotLinks;
IF OBJECT_ID(N'app.RefreshTokens', N'U') IS NOT NULL DROP TABLE app.RefreshTokens;
IF OBJECT_ID(N'app.Payments', N'U') IS NOT NULL DROP TABLE app.Payments;
IF OBJECT_ID(N'app.Subscriptions', N'U') IS NOT NULL DROP TABLE app.Subscriptions;
IF OBJECT_ID(N'app.SubscriptionPlans', N'U') IS NOT NULL DROP TABLE app.SubscriptionPlans;
IF OBJECT_ID(N'app.ExpenseAttachments', N'U') IS NOT NULL DROP TABLE app.ExpenseAttachments;
IF OBJECT_ID(N'app.ApprovalRequests', N'U') IS NOT NULL DROP TABLE app.ApprovalRequests;
IF OBJECT_ID(N'app.Expenses', N'U') IS NOT NULL DROP TABLE app.Expenses;
IF OBJECT_ID(N'app.Payees', N'U') IS NOT NULL DROP TABLE app.Payees;
IF OBJECT_ID(N'app.ExpenseCategories', N'U') IS NOT NULL DROP TABLE app.ExpenseCategories;
IF OBJECT_ID(N'app.BotSessions', N'U') IS NOT NULL DROP TABLE app.BotSessions;
IF OBJECT_ID(N'app.AuditLogs', N'U') IS NOT NULL DROP TABLE app.AuditLogs;
IF OBJECT_ID(N'app.Users', N'U') IS NOT NULL DROP TABLE app.Users;
IF OBJECT_ID(N'app.Tenants', N'U') IS NOT NULL DROP TABLE app.Tenants;
GO

CREATE TABLE app.Tenants
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    PublicId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Name                NVARCHAR(160) NOT NULL,
    Slug                NVARCHAR(120) NOT NULL,
    Email               NVARCHAR(180) NULL,
    Phone               NVARCHAR(30) NULL,
    CountryCode         NVARCHAR(10) NOT NULL DEFAULT N'MX',
    TimeZone            NVARCHAR(80) NOT NULL DEFAULT N'America/Mexico_City',
    CurrencyCode        NVARCHAR(10) NOT NULL DEFAULT N'MXN',
    Status              NVARCHAR(30) NOT NULL DEFAULT N'ACTIVE',
    TrialEndsAt         DATETIME2 NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Tenants_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Tenants_Slug UNIQUE (Slug)
);
GO

CREATE TABLE app.Users
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    PublicId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    FirstName           NVARCHAR(120) NOT NULL,
    LastName            NVARCHAR(120) NULL,
    Email               NVARCHAR(180) NOT NULL,
    PasswordHash        NVARCHAR(500) NOT NULL,
    PasswordAlgorithm   NVARCHAR(30) NOT NULL DEFAULT N'ARGON2ID',
    EmailConfirmed      BIT NOT NULL DEFAULT 0,
    IsPlatformAdmin     BIT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    LastLoginAt         DATETIME2 NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

CREATE TABLE app.TenantUsers
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    TenantId            INT NOT NULL,
    UserId              INT NOT NULL,
    RoleCode            NVARCHAR(40) NOT NULL,
    IsOwner             BIT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_TenantUsers_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT FK_TenantUsers_Users FOREIGN KEY (UserId) REFERENCES app.Users(Id),
    CONSTRAINT UQ_TenantUsers_Tenant_User UNIQUE (TenantId, UserId)
);
GO

CREATE TABLE app.SubscriptionPlans
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    Code                NVARCHAR(30) NOT NULL,
    Name                NVARCHAR(100) NOT NULL,
    PriceMonthly        DECIMAL(18,2) NOT NULL,
    CurrencyCode        NVARCHAR(10) NOT NULL DEFAULT N'MXN',
    MaxUsers            INT NOT NULL,
    MaxBranches         INT NOT NULL DEFAULT 1,
    HasApprovals        BIT NOT NULL DEFAULT 0,
    HasAdvancedReports  BIT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_SubscriptionPlans_Code UNIQUE (Code)
);
GO

CREATE TABLE app.Subscriptions
(
    Id                      INT IDENTITY(1,1) PRIMARY KEY,
    TenantId                INT NOT NULL,
    PlanId                  INT NOT NULL,
    Provider                NVARCHAR(30) NOT NULL,
    ProviderCustomerId      NVARCHAR(120) NULL,
    ProviderSubscriptionId  NVARCHAR(120) NULL,
    Status                  NVARCHAR(30) NOT NULL DEFAULT N'TRIALING',
    CurrentPeriodStart      DATETIME2 NULL,
    CurrentPeriodEnd        DATETIME2 NULL,
    CancelAtPeriodEnd       BIT NOT NULL DEFAULT 0,
    CanceledAt              DATETIME2 NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Subscriptions_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT FK_Subscriptions_Plans FOREIGN KEY (PlanId) REFERENCES app.SubscriptionPlans(Id)
);
GO

CREATE TABLE app.Payments
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    SubscriptionId      INT NOT NULL,
    Provider            NVARCHAR(30) NOT NULL,
    ProviderPaymentId   NVARCHAR(120) NULL,
    Amount              DECIMAL(18,2) NOT NULL,
    CurrencyCode        NVARCHAR(10) NOT NULL DEFAULT N'MXN',
    Status              NVARCHAR(30) NOT NULL,
    PaidAt              DATETIME2 NULL,
    RawJson             NVARCHAR(MAX) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Payments_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES app.Subscriptions(Id)
);
GO

CREATE TABLE app.RefreshTokens
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    UserId              INT NOT NULL,
    TenantId            INT NOT NULL,
    TokenHash           NVARCHAR(500) NOT NULL,
    ExpiresAt           DATETIME2 NOT NULL,
    RevokedAt           DATETIME2 NULL,
    ReplacedByTokenHash NVARCHAR(500) NULL,
    CreatedByIp         NVARCHAR(64) NULL,
    RevokedByIp         NVARCHAR(64) NULL,
    UserAgent           NVARCHAR(500) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES app.Users(Id),
    CONSTRAINT FK_RefreshTokens_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id)
);
GO

CREATE TABLE app.BotLinks
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    TenantId            INT NOT NULL,
    UserId              INT NOT NULL,
    Channel             NVARCHAR(20) NOT NULL,
    ExternalUserId      NVARCHAR(120) NULL,
    ExternalChatId      NVARCHAR(120) NULL,
    PhoneNumber         NVARCHAR(30) NULL,
    Username            NVARCHAR(120) NULL,
    LinkCode            NVARCHAR(120) NULL,
    LinkedAt            DATETIME2 NULL,
    IsActive            BIT NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BotLinks_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT FK_BotLinks_Users FOREIGN KEY (UserId) REFERENCES app.Users(Id)
);
GO

CREATE TABLE app.ExpenseCategories
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    TenantId            INT NOT NULL,
    Name                NVARCHAR(140) NOT NULL,
    Description         NVARCHAR(240) NULL,
    ColorHex            NVARCHAR(10) NULL,
    IsActive            BIT NOT NULL DEFAULT 1,
    SortOrder           INT NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ExpenseCategories_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT UQ_ExpenseCategories_Tenant_Name UNIQUE (TenantId, Name)
);
GO

CREATE TABLE app.Payees
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    TenantId            INT NOT NULL,
    TypeCode            NVARCHAR(30) NOT NULL,
    Name                NVARCHAR(180) NOT NULL,
    TaxId               NVARCHAR(30) NULL,
    Email               NVARCHAR(180) NULL,
    Phone               NVARCHAR(30) NULL,
    IsActive            BIT NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Payees_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id)
);
GO

CREATE TABLE app.Expenses
(
    Id                      INT IDENTITY(1,1) PRIMARY KEY,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    TenantId                INT NOT NULL,
    CreatedByUserId         INT NOT NULL,
    CategoryId              INT NULL,
    PayeeId                 INT NULL,
    Status                  NVARCHAR(30) NOT NULL DEFAULT N'DRAFT',
    ExpenseDate             DATE NOT NULL,
    Description             NVARCHAR(300) NOT NULL,
    Notes                   NVARCHAR(600) NULL,
    AmountSubtotal          DECIMAL(18,2) NOT NULL,
    IvaAmount               DECIMAL(18,2) NOT NULL DEFAULT 0,
    AmountTotal             DECIMAL(18,2) NOT NULL,
    CurrencyCode            NVARCHAR(10) NOT NULL DEFAULT N'MXN',
    PaymentMethod           NVARCHAR(30) NULL,
    BranchName              NVARCHAR(140) NULL,
    CostCenter              NVARCHAR(140) NULL,
    ProjectName             NVARCHAR(140) NULL,
    SubmittedAt             DATETIME2 NULL,
    ApprovedAt              DATETIME2 NULL,
    RejectedAt              DATETIME2 NULL,
    AccountedAt             DATETIME2 NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Expenses_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT FK_Expenses_Users FOREIGN KEY (CreatedByUserId) REFERENCES app.Users(Id),
    CONSTRAINT FK_Expenses_Categories FOREIGN KEY (CategoryId) REFERENCES app.ExpenseCategories(Id),
    CONSTRAINT FK_Expenses_Payees FOREIGN KEY (PayeeId) REFERENCES app.Payees(Id),
    CONSTRAINT UQ_Expenses_PublicId UNIQUE (PublicId)
);
GO

CREATE TABLE app.ExpenseAttachments
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    ExpenseId           INT NOT NULL,
    FileName            NVARCHAR(260) NOT NULL,
    ContentType         NVARCHAR(120) NOT NULL,
    StorageProvider     NVARCHAR(30) NOT NULL,
    StorageUrl          NVARCHAR(500) NOT NULL,
    OcrText             NVARCHAR(MAX) NULL,
    UploadedAt          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ExpenseAttachments_Expenses FOREIGN KEY (ExpenseId) REFERENCES app.Expenses(Id)
);
GO

CREATE TABLE app.ApprovalRequests
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    ExpenseId           INT NOT NULL,
    RequestedByUserId   INT NOT NULL,
    ApproverUserId      INT NOT NULL,
    Status              NVARCHAR(30) NOT NULL DEFAULT N'PENDING',
    RequestedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RespondedAt         DATETIME2 NULL,
    Comments            NVARCHAR(500) NULL,
    CONSTRAINT FK_ApprovalRequests_Expenses FOREIGN KEY (ExpenseId) REFERENCES app.Expenses(Id),
    CONSTRAINT FK_ApprovalRequests_RequestedBy FOREIGN KEY (RequestedByUserId) REFERENCES app.Users(Id),
    CONSTRAINT FK_ApprovalRequests_Approver FOREIGN KEY (ApproverUserId) REFERENCES app.Users(Id)
);
GO

CREATE TABLE app.BotSessions
(
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    TenantId            INT NOT NULL,
    UserId              INT NOT NULL,
    Channel             NVARCHAR(20) NOT NULL,
    ChatId              NVARCHAR(120) NOT NULL,
    CurrentState        NVARCHAR(60) NOT NULL,
    SessionJson         NVARCHAR(MAX) NULL,
    LastInteractionAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BotSessions_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT FK_BotSessions_Users FOREIGN KEY (UserId) REFERENCES app.Users(Id),
    CONSTRAINT UQ_BotSessions_Channel_Chat UNIQUE (Channel, ChatId)
);
GO

CREATE TABLE app.AuditLogs
(
    Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    TenantId            INT NULL,
    UserId              INT NULL,
    EntityName          NVARCHAR(100) NOT NULL,
    EntityId            NVARCHAR(100) NOT NULL,
    ActionCode          NVARCHAR(40) NOT NULL,
    Description         NVARCHAR(500) NULL,
    IpAddress           NVARCHAR(64) NULL,
    UserAgent           NVARCHAR(500) NULL,
    MetadataJson        NVARCHAR(MAX) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditLogs_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants(Id),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES app.Users(Id)
);
GO

CREATE INDEX IX_TenantUsers_TenantId ON app.TenantUsers(TenantId);
CREATE INDEX IX_TenantUsers_UserId ON app.TenantUsers(UserId);
CREATE INDEX IX_Subscriptions_TenantId ON app.Subscriptions(TenantId);
CREATE INDEX IX_Payments_SubscriptionId ON app.Payments(SubscriptionId);
CREATE INDEX IX_RefreshTokens_UserId ON app.RefreshTokens(UserId);
CREATE INDEX IX_RefreshTokens_TenantId ON app.RefreshTokens(TenantId);
CREATE INDEX IX_BotLinks_TenantId ON app.BotLinks(TenantId);
CREATE INDEX IX_BotLinks_UserId ON app.BotLinks(UserId);
CREATE INDEX IX_BotLinks_ExternalChatId ON app.BotLinks(ExternalChatId);
CREATE INDEX IX_BotLinks_PhoneNumber ON app.BotLinks(PhoneNumber);
CREATE INDEX IX_ExpenseCategories_TenantId ON app.ExpenseCategories(TenantId);
CREATE INDEX IX_Payees_TenantId ON app.Payees(TenantId);
CREATE INDEX IX_Expenses_TenantId ON app.Expenses(TenantId);
CREATE INDEX IX_Expenses_CreatedByUserId ON app.Expenses(CreatedByUserId);
CREATE INDEX IX_Expenses_ExpenseDate ON app.Expenses(ExpenseDate);
CREATE INDEX IX_Expenses_Status ON app.Expenses(Status);
CREATE INDEX IX_ExpenseAttachments_ExpenseId ON app.ExpenseAttachments(ExpenseId);
CREATE INDEX IX_ApprovalRequests_ExpenseId ON app.ApprovalRequests(ExpenseId);
CREATE INDEX IX_BotSessions_TenantId ON app.BotSessions(TenantId);
CREATE INDEX IX_AuditLogs_TenantId ON app.AuditLogs(TenantId);
GO

INSERT INTO app.SubscriptionPlans (Code, Name, PriceMonthly, CurrencyCode, MaxUsers, MaxBranches, HasApprovals, HasAdvancedReports)
VALUES
    (N'PERSONAL', N'Personal', 99.00, N'MXN', 1, 1, 0, 0),
    (N'NEGOCIO', N'Negocio', 199.00, N'MXN', 5, 3, 1, 0),
    (N'EQUIPO', N'Equipo', 499.00, N'MXN', 25, 20, 1, 1);
GO

