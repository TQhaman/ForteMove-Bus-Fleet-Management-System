SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.UserAccounts', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.StaffProfiles', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.AuditEntries', N'U') IS NOT NULL
BEGIN
    THROW 51001, 'Identity and access objects already exist but migration 0001 is not recorded.', 1;
END;

CREATE TABLE dbo.Roles
(
    RoleId INT NOT NULL,
    RoleCode NVARCHAR(50) NOT NULL,
    DisplayName NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_Roles_IsActive DEFAULT (1),
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Roles_CreatedUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),
    CONSTRAINT UQ_Roles_RoleCode UNIQUE (RoleCode)
);

CREATE TABLE dbo.UserAccounts
(
    UserAccountId BIGINT IDENTITY(1, 1) NOT NULL,
    RoleId INT NOT NULL,
    Email NVARCHAR(254) NOT NULL,
    NormalizedEmail NVARCHAR(254) NOT NULL,
    PasswordAlgorithm NVARCHAR(50) NOT NULL,
    PasswordHash VARBINARY(64) NOT NULL,
    PasswordSalt VARBINARY(64) NOT NULL,
    PasswordIterations INT NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_UserAccounts_IsActive DEFAULT (1),
    MustChangePassword BIT NOT NULL
        CONSTRAINT DF_UserAccounts_MustChangePassword DEFAULT (0),
    FailedLoginCount INT NOT NULL
        CONSTRAINT DF_UserAccounts_FailedLoginCount DEFAULT (0),
    LockoutEndUtc DATETIME2(0) NULL,
    LastLoginUtc DATETIME2(0) NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_UserAccounts_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_UserAccounts_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_UserAccounts PRIMARY KEY CLUSTERED (UserAccountId),
    CONSTRAINT FK_UserAccounts_Roles
        FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT UQ_UserAccounts_NormalizedEmail UNIQUE (NormalizedEmail),
    CONSTRAINT CK_UserAccounts_PasswordIterations
        CHECK (PasswordIterations > 0),
    CONSTRAINT CK_UserAccounts_PasswordHash_Length
        CHECK (DATALENGTH(PasswordHash) BETWEEN 16 AND 64),
    CONSTRAINT CK_UserAccounts_PasswordSalt_Length
        CHECK (DATALENGTH(PasswordSalt) BETWEEN 16 AND 64),
    CONSTRAINT CK_UserAccounts_FailedLoginCount
        CHECK (FailedLoginCount >= 0)
);

CREATE INDEX IX_UserAccounts_RoleId
    ON dbo.UserAccounts (RoleId);

CREATE TABLE dbo.StaffProfiles
(
    StaffProfileId BIGINT IDENTITY(1, 1) NOT NULL,
    UserAccountId BIGINT NOT NULL,
    EmployeeNumber NVARCHAR(50) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(30) NULL,
    EmploymentStatus NVARCHAR(30) NOT NULL
        CONSTRAINT DF_StaffProfiles_EmploymentStatus DEFAULT (N'Active'),
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_StaffProfiles_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_StaffProfiles_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_StaffProfiles PRIMARY KEY CLUSTERED (StaffProfileId),
    CONSTRAINT FK_StaffProfiles_UserAccounts
        FOREIGN KEY (UserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_StaffProfiles_UserAccountId UNIQUE (UserAccountId),
    CONSTRAINT UQ_StaffProfiles_EmployeeNumber UNIQUE (EmployeeNumber)
);

CREATE TABLE dbo.AuditEntries
(
    AuditEntryId BIGINT IDENTITY(1, 1) NOT NULL,
    ActorUserAccountId BIGINT NULL,
    EventType NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(100) NULL,
    Detail NVARCHAR(1000) NULL,
    ClientIpAddress NVARCHAR(45) NULL,
    OccurredUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_AuditEntries_OccurredUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditEntries PRIMARY KEY CLUSTERED (AuditEntryId),
    CONSTRAINT FK_AuditEntries_UserAccounts
        FOREIGN KEY (ActorUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId)
);

CREATE INDEX IX_AuditEntries_ActorUserAccountId_OccurredUtc
    ON dbo.AuditEntries (ActorUserAccountId, OccurredUtc DESC);

CREATE INDEX IX_AuditEntries_EntityType_EntityId
    ON dbo.AuditEntries (EntityType, EntityId);

INSERT dbo.Roles (RoleId, RoleCode, DisplayName, IsActive)
VALUES
    (1, N'TransportAdministrator', N'Transport Administrator', 1),
    (2, N'Driver', N'Driver', 1),
    (3, N'Passenger', N'Passenger', 1);
