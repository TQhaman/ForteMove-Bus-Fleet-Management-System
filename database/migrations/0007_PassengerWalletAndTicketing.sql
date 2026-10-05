SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.PassengerProfiles', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.PassengerWallets', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.Tickets', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.WalletTransactions', N'U') IS NOT NULL
BEGIN
    THROW 51050, 'Passenger wallet or ticketing objects already exist but migration 0007 is not recorded.', 1;
END;

IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Roles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Routes', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Trips', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TripAssignments', N'U') IS NULL
BEGIN
    THROW 51051, 'The identity, route, scheduling, or assignment foundation is missing.', 1;
END;

CREATE TABLE dbo.PassengerProfiles
(
    PassengerProfileId BIGINT IDENTITY(1, 1) NOT NULL,
    UserAccountId BIGINT NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(30) NULL,
    CreatedUtc DATETIME2(0) NOT NULL,
    UpdatedUtc DATETIME2(0) NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_PassengerProfiles PRIMARY KEY CLUSTERED (PassengerProfileId),
    CONSTRAINT UQ_PassengerProfiles_UserAccountId UNIQUE (UserAccountId),
    CONSTRAINT FK_PassengerProfiles_UserAccounts
        FOREIGN KEY (UserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_PassengerProfiles_FirstName CHECK (LEN(LTRIM(RTRIM(FirstName))) > 0),
    CONSTRAINT CK_PassengerProfiles_LastName CHECK (LEN(LTRIM(RTRIM(LastName))) > 0),
    CONSTRAINT CK_PassengerProfiles_Phone CHECK
        (PhoneNumber IS NULL OR LEN(LTRIM(RTRIM(PhoneNumber))) > 0)
);

CREATE TABLE dbo.PassengerWallets
(
    PassengerWalletId BIGINT IDENTITY(1, 1) NOT NULL,
    PassengerProfileId BIGINT NOT NULL,
    CurrentBalance DECIMAL(12, 2) NOT NULL
        CONSTRAINT DF_PassengerWallets_CurrentBalance DEFAULT (0),
    CreatedUtc DATETIME2(0) NOT NULL,
    UpdatedUtc DATETIME2(0) NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_PassengerWallets PRIMARY KEY CLUSTERED (PassengerWalletId),
    CONSTRAINT UQ_PassengerWallets_PassengerProfileId UNIQUE (PassengerProfileId),
    CONSTRAINT FK_PassengerWallets_PassengerProfiles
        FOREIGN KEY (PassengerProfileId) REFERENCES dbo.PassengerProfiles (PassengerProfileId),
    CONSTRAINT CK_PassengerWallets_CurrentBalance CHECK (CurrentBalance >= 0)
);

CREATE TABLE dbo.Tickets
(
    TicketId BIGINT IDENTITY(1, 1) NOT NULL,
    TicketCode NVARCHAR(20) NOT NULL,
    PassengerProfileId BIGINT NOT NULL,
    TripId BIGINT NOT NULL,
    FareAmount DECIMAL(12, 2) NOT NULL,
    TicketStatus NVARCHAR(20) NOT NULL,
    PurchasedUtc DATETIME2(0) NOT NULL,
    RefundedUtc DATETIME2(0) NULL,
    RefundedByUserAccountId BIGINT NULL,
    RefundReason NVARCHAR(500) NULL,
    UpdatedUtc DATETIME2(0) NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Tickets PRIMARY KEY CLUSTERED (TicketId),
    CONSTRAINT UQ_Tickets_TicketCode UNIQUE (TicketCode),
    CONSTRAINT FK_Tickets_PassengerProfiles
        FOREIGN KEY (PassengerProfileId) REFERENCES dbo.PassengerProfiles (PassengerProfileId),
    CONSTRAINT FK_Tickets_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_Tickets_RefundedBy
        FOREIGN KEY (RefundedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_Tickets_Code CHECK (LEN(LTRIM(RTRIM(TicketCode))) > 0),
    CONSTRAINT CK_Tickets_FareAmount CHECK (FareAmount >= 0),
    CONSTRAINT CK_Tickets_Status CHECK (TicketStatus IN (N'Purchased', N'Refunded')),
    CONSTRAINT CK_Tickets_RefundState CHECK
    (
        (TicketStatus = N'Purchased' AND RefundedUtc IS NULL
            AND RefundedByUserAccountId IS NULL AND RefundReason IS NULL)
        OR
        (TicketStatus = N'Refunded' AND RefundedUtc IS NOT NULL
            AND RefundedByUserAccountId IS NOT NULL
            AND RefundReason IS NOT NULL AND LEN(LTRIM(RTRIM(RefundReason))) > 0)
    )
);

CREATE UNIQUE INDEX UX_Tickets_PassengerTrip_Purchased
    ON dbo.Tickets (PassengerProfileId, TripId)
    WHERE TicketStatus = N'Purchased';

CREATE INDEX IX_Tickets_PassengerStatusDeparture
    ON dbo.Tickets (PassengerProfileId, TicketStatus, TripId)
    INCLUDE (TicketCode, FareAmount, PurchasedUtc);

CREATE INDEX IX_Tickets_TripStatus
    ON dbo.Tickets (TripId, TicketStatus)
    INCLUDE (PassengerProfileId, FareAmount, TicketCode);

CREATE TABLE dbo.WalletTransactions
(
    WalletTransactionId BIGINT IDENTITY(1, 1) NOT NULL,
    WalletTransactionCode NVARCHAR(20) NOT NULL,
    PassengerWalletId BIGINT NOT NULL,
    TransactionType NVARCHAR(30) NOT NULL,
    Amount DECIMAL(12, 2) NOT NULL,
    BalanceBefore DECIMAL(12, 2) NOT NULL,
    BalanceAfter DECIMAL(12, 2) NOT NULL,
    TicketId BIGINT NULL,
    InitiatedByUserAccountId BIGINT NOT NULL,
    OperationToken UNIQUEIDENTIFIER NOT NULL,
    OccurredUtc DATETIME2(0) NOT NULL,
    CONSTRAINT PK_WalletTransactions PRIMARY KEY CLUSTERED (WalletTransactionId),
    CONSTRAINT UQ_WalletTransactions_Code UNIQUE (WalletTransactionCode),
    CONSTRAINT UQ_WalletTransactions_OperationToken UNIQUE (OperationToken),
    CONSTRAINT FK_WalletTransactions_PassengerWallets
        FOREIGN KEY (PassengerWalletId) REFERENCES dbo.PassengerWallets (PassengerWalletId),
    CONSTRAINT FK_WalletTransactions_Tickets
        FOREIGN KEY (TicketId) REFERENCES dbo.Tickets (TicketId),
    CONSTRAINT FK_WalletTransactions_InitiatedBy
        FOREIGN KEY (InitiatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_WalletTransactions_Code
        CHECK (LEN(LTRIM(RTRIM(WalletTransactionCode))) > 0),
    CONSTRAINT CK_WalletTransactions_Type CHECK
        (TransactionType IN (N'SimulatedTopUp', N'TicketPurchase', N'TripCancellationRefund')),
    CONSTRAINT CK_WalletTransactions_Balances CHECK
        (BalanceBefore >= 0 AND BalanceAfter >= 0),
    CONSTRAINT CK_WalletTransactions_Arithmetic CHECK
    (
        (TransactionType = N'SimulatedTopUp' AND TicketId IS NULL AND Amount > 0
            AND BalanceAfter = BalanceBefore + Amount)
        OR
        (TransactionType = N'TicketPurchase' AND TicketId IS NOT NULL AND Amount >= 0
            AND BalanceAfter = BalanceBefore - Amount)
        OR
        (TransactionType = N'TripCancellationRefund' AND TicketId IS NOT NULL AND Amount >= 0
            AND BalanceAfter = BalanceBefore + Amount)
    )
);

CREATE UNIQUE INDEX UX_WalletTransactions_TicketType
    ON dbo.WalletTransactions (TicketId, TransactionType)
    WHERE TicketId IS NOT NULL;

CREATE INDEX IX_WalletTransactions_WalletOccurred
    ON dbo.WalletTransactions (PassengerWalletId, OccurredUtc DESC, WalletTransactionId DESC)
    INCLUDE (WalletTransactionCode, TransactionType, Amount, BalanceAfter, TicketId);
