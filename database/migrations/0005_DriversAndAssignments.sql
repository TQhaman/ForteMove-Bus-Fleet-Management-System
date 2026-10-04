SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.DriverProfiles', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.TripAssignments', N'U') IS NOT NULL
   OR COL_LENGTH(N'dbo.Buses', N'GrossVehicleMassKg') IS NOT NULL
BEGIN
    THROW 51010, 'Driver or assignment objects already exist but migration 0005 is not recorded.', 1;
END;

IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
   OR OBJECT_ID(N'dbo.StaffProfiles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Buses', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Trips', N'U') IS NULL
BEGIN
    THROW 51011, 'The identity, fleet, or scheduling foundation is missing. Migration 0005 cannot continue.', 1;
END;

ALTER TABLE dbo.Buses
ADD GrossVehicleMassKg INT NULL;

EXEC sys.sp_executesql N'
ALTER TABLE dbo.Buses
ADD CONSTRAINT CK_Buses_GrossVehicleMassKg
    CHECK (GrossVehicleMassKg IS NULL OR GrossVehicleMassKg > 0);';

CREATE TABLE dbo.DriverProfiles
(
    DriverProfileId BIGINT IDENTITY(1, 1) NOT NULL,
    StaffProfileId BIGINT NOT NULL,
    DateOfBirth DATE NOT NULL,
    AvailabilityStatus NVARCHAR(30) NOT NULL,
    LicenceNumber NVARCHAR(50) NOT NULL,
    LicenceCode NVARCHAR(5) NOT NULL,
    LicenceExpiryDate DATE NOT NULL,
    PrdpNumber NVARCHAR(50) NOT NULL,
    PrdpCategory NVARCHAR(5) NOT NULL,
    PrdpExpiryDate DATE NOT NULL,
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_DriverProfiles_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_DriverProfiles_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_DriverProfiles PRIMARY KEY CLUSTERED (DriverProfileId),
    CONSTRAINT FK_DriverProfiles_StaffProfiles
        FOREIGN KEY (StaffProfileId) REFERENCES dbo.StaffProfiles (StaffProfileId),
    CONSTRAINT FK_DriverProfiles_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_DriverProfiles_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_DriverProfiles_StaffProfileId UNIQUE (StaffProfileId),
    CONSTRAINT UQ_DriverProfiles_LicenceNumber UNIQUE (LicenceNumber),
    CONSTRAINT UQ_DriverProfiles_PrdpNumber UNIQUE (PrdpNumber),
    CONSTRAINT CK_DriverProfiles_AvailabilityStatus
        CHECK (AvailabilityStatus IN (N'Available', N'Unavailable', N'OnLeave', N'Suspended')),
    CONSTRAINT CK_DriverProfiles_LicenceCode
        CHECK (LicenceCode IN (N'B', N'EB', N'C1', N'C', N'EC1', N'EC')),
    CONSTRAINT CK_DriverProfiles_PrdpCategory
        CHECK (PrdpCategory = N'P'),
    CONSTRAINT CK_DriverProfiles_LicenceNumber_NotBlank
        CHECK (LEN(LTRIM(RTRIM(LicenceNumber))) > 0),
    CONSTRAINT CK_DriverProfiles_PrdpNumber_NotBlank
        CHECK (LEN(LTRIM(RTRIM(PrdpNumber))) > 0)
);

CREATE INDEX IX_DriverProfiles_Availability_Expiry
    ON dbo.DriverProfiles (AvailabilityStatus, LicenceExpiryDate, PrdpExpiryDate);

CREATE TABLE dbo.TripAssignments
(
    TripAssignmentId BIGINT IDENTITY(1, 1) NOT NULL,
    TripId BIGINT NOT NULL,
    DriverProfileId BIGINT NOT NULL,
    BusId BIGINT NOT NULL,
    IsCurrent BIT NOT NULL,
    DecisionType NVARCHAR(30) NOT NULL,
    DecisionReason NVARCHAR(500) NULL,
    AssignedByUserAccountId BIGINT NOT NULL,
    AssignedUtc DATETIME2(0) NOT NULL,
    EndType NVARCHAR(20) NULL,
    EndReason NVARCHAR(500) NULL,
    EndedByUserAccountId BIGINT NULL,
    EndedUtc DATETIME2(0) NULL,
    UpdatedUtc DATETIME2(0) NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TripAssignments PRIMARY KEY CLUSTERED (TripAssignmentId),
    CONSTRAINT FK_TripAssignments_Trips
        FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_TripAssignments_DriverProfiles
        FOREIGN KEY (DriverProfileId) REFERENCES dbo.DriverProfiles (DriverProfileId),
    CONSTRAINT FK_TripAssignments_Buses
        FOREIGN KEY (BusId) REFERENCES dbo.Buses (BusId),
    CONSTRAINT FK_TripAssignments_AssignedByUserAccounts
        FOREIGN KEY (AssignedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_TripAssignments_EndedByUserAccounts
        FOREIGN KEY (EndedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_TripAssignments_DecisionType
        CHECK (DecisionType IN (N'RecommendationAccepted', N'AlternativeSelected', N'Change')),
    CONSTRAINT CK_TripAssignments_EndType
        CHECK (EndType IS NULL OR EndType IN (N'Changed', N'Removed')),
    CONSTRAINT CK_TripAssignments_DecisionReason
        CHECK
        (
            (DecisionType = N'RecommendationAccepted' AND
                (DecisionReason IS NULL OR LEN(LTRIM(RTRIM(DecisionReason))) > 0))
            OR
            (DecisionType IN (N'AlternativeSelected', N'Change') AND
                DecisionReason IS NOT NULL AND LEN(LTRIM(RTRIM(DecisionReason))) > 0)
        ),
    CONSTRAINT CK_TripAssignments_CurrentEndState
        CHECK
        (
            (IsCurrent = 1 AND EndType IS NULL AND EndReason IS NULL
                AND EndedByUserAccountId IS NULL AND EndedUtc IS NULL)
            OR
            (IsCurrent = 0 AND EndType IS NOT NULL
                AND EndReason IS NOT NULL AND LEN(LTRIM(RTRIM(EndReason))) > 0
                AND EndedByUserAccountId IS NOT NULL AND EndedUtc IS NOT NULL)
        )
);

CREATE UNIQUE INDEX UX_TripAssignments_CurrentTrip
    ON dbo.TripAssignments (TripId)
    WHERE IsCurrent = 1;

CREATE INDEX IX_TripAssignments_CurrentDriver
    ON dbo.TripAssignments (DriverProfileId, IsCurrent, TripId)
    INCLUDE (BusId)
    WHERE IsCurrent = 1;

CREATE INDEX IX_TripAssignments_CurrentBus
    ON dbo.TripAssignments (BusId, IsCurrent, TripId)
    INCLUDE (DriverProfileId)
    WHERE IsCurrent = 1;

CREATE INDEX IX_TripAssignments_TripHistory
    ON dbo.TripAssignments (TripId, AssignedUtc DESC, TripAssignmentId DESC);
