SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.RouteSchedules', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.RouteScheduleVersions', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.ScheduleOperatingDays', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.ScheduleDepartureTimes', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.Trips', N'U') IS NOT NULL
BEGIN
    THROW 51007, 'Scheduling objects already exist but migration 0004 is not recorded.', 1;
END;

IF OBJECT_ID(N'dbo.Routes', N'U') IS NULL
   OR OBJECT_ID(N'dbo.BusCategories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
BEGIN
    THROW 51008, 'The route, fleet, or identity foundation is missing. Migration 0004 cannot continue.', 1;
END;

IF EXISTS
(
    SELECT RequiredConstraint.ConstraintName
    FROM
    (
        VALUES
            (N'CK_Stops_Coordinates_Paired'),
            (N'CK_Stops_Latitude'),
            (N'CK_Stops_Longitude')
    ) AS RequiredConstraint (ConstraintName)
    LEFT JOIN sys.check_constraints AS ExistingConstraint
        ON ExistingConstraint.parent_object_id = OBJECT_ID(N'dbo.Stops', N'U')
       AND ExistingConstraint.name = RequiredConstraint.ConstraintName
       AND ExistingConstraint.is_disabled = 0
       AND ExistingConstraint.is_not_trusted = 0
    WHERE ExistingConstraint.object_id IS NULL
)
BEGIN
    THROW 51009, 'The trusted Stop coordinate safety constraints are missing or disabled.', 1;
END;

CREATE TABLE dbo.RouteSchedules
(
    RouteScheduleId BIGINT IDENTITY(1, 1) NOT NULL,
    ScheduleCode NVARCHAR(20) NOT NULL,
    RouteId BIGINT NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_RouteSchedules_IsActive DEFAULT (1),
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_RouteSchedules_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_RouteSchedules_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_RouteSchedules PRIMARY KEY CLUSTERED (RouteScheduleId),
    CONSTRAINT FK_RouteSchedules_Routes
        FOREIGN KEY (RouteId) REFERENCES dbo.Routes (RouteId),
    CONSTRAINT FK_RouteSchedules_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_RouteSchedules_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_RouteSchedules_ScheduleCode UNIQUE (ScheduleCode),
    CONSTRAINT UQ_RouteSchedules_Id_Route UNIQUE (RouteScheduleId, RouteId),
    CONSTRAINT CK_RouteSchedules_ScheduleCode_NotBlank
        CHECK (LEN(LTRIM(RTRIM(ScheduleCode))) > 0)
);

CREATE INDEX IX_RouteSchedules_Route_IsActive
    ON dbo.RouteSchedules (RouteId, IsActive);

CREATE TABLE dbo.RouteScheduleVersions
(
    RouteScheduleVersionId BIGINT IDENTITY(1, 1) NOT NULL,
    RouteScheduleId BIGINT NOT NULL,
    RouteId BIGINT NOT NULL,
    VersionNumber INT NOT NULL,
    EffectiveStartDate DATE NOT NULL,
    EffectiveEndDate DATE NOT NULL,
    PreferredBusCategoryId INT NULL,
    ExpectedCapacity INT NULL,
    SupersededFromDate DATE NULL,
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_RouteScheduleVersions_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_RouteScheduleVersions_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_RouteScheduleVersions PRIMARY KEY CLUSTERED (RouteScheduleVersionId),
    CONSTRAINT FK_RouteScheduleVersions_RouteSchedules
        FOREIGN KEY (RouteScheduleId, RouteId)
        REFERENCES dbo.RouteSchedules (RouteScheduleId, RouteId),
    CONSTRAINT FK_RouteScheduleVersions_Routes
        FOREIGN KEY (RouteId) REFERENCES dbo.Routes (RouteId),
    CONSTRAINT FK_RouteScheduleVersions_BusCategories
        FOREIGN KEY (PreferredBusCategoryId) REFERENCES dbo.BusCategories (BusCategoryId),
    CONSTRAINT FK_RouteScheduleVersions_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_RouteScheduleVersions_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_RouteScheduleVersions_Schedule_Version
        UNIQUE (RouteScheduleId, VersionNumber),
    CONSTRAINT UQ_RouteScheduleVersions_Id_Route
        UNIQUE (RouteScheduleVersionId, RouteId),
    CONSTRAINT CK_RouteScheduleVersions_VersionNumber
        CHECK (VersionNumber >= 1),
    CONSTRAINT CK_RouteScheduleVersions_EffectivePeriod
        CHECK (EffectiveEndDate >= EffectiveStartDate),
    CONSTRAINT CK_RouteScheduleVersions_ExpectedCapacity
        CHECK (ExpectedCapacity IS NULL OR ExpectedCapacity > 0),
    CONSTRAINT CK_RouteScheduleVersions_SupersededFromDate
        CHECK
        (
            SupersededFromDate IS NULL
            OR SupersededFromDate BETWEEN EffectiveStartDate AND EffectiveEndDate
        )
);

CREATE UNIQUE INDEX UX_RouteScheduleVersions_Current
    ON dbo.RouteScheduleVersions (RouteScheduleId)
    WHERE SupersededFromDate IS NULL;

CREATE INDEX IX_RouteScheduleVersions_EffectivePeriod
    ON dbo.RouteScheduleVersions (EffectiveStartDate, EffectiveEndDate, RouteScheduleId);

CREATE TABLE dbo.ScheduleOperatingDays
(
    RouteScheduleVersionId BIGINT NOT NULL,
    DayOfWeek TINYINT NOT NULL,
    CONSTRAINT PK_ScheduleOperatingDays
        PRIMARY KEY CLUSTERED (RouteScheduleVersionId, DayOfWeek),
    CONSTRAINT FK_ScheduleOperatingDays_RouteScheduleVersions
        FOREIGN KEY (RouteScheduleVersionId)
        REFERENCES dbo.RouteScheduleVersions (RouteScheduleVersionId),
    CONSTRAINT CK_ScheduleOperatingDays_DayOfWeek
        CHECK (DayOfWeek BETWEEN 0 AND 6)
);

CREATE TABLE dbo.ScheduleDepartureTimes
(
    RouteScheduleVersionId BIGINT NOT NULL,
    DepartureTime TIME(0) NOT NULL,
    CONSTRAINT PK_ScheduleDepartureTimes
        PRIMARY KEY CLUSTERED (RouteScheduleVersionId, DepartureTime),
    CONSTRAINT FK_ScheduleDepartureTimes_RouteScheduleVersions
        FOREIGN KEY (RouteScheduleVersionId)
        REFERENCES dbo.RouteScheduleVersions (RouteScheduleVersionId),
    CONSTRAINT CK_ScheduleDepartureTimes_MinutePrecision
        CHECK (DATEPART(SECOND, DepartureTime) = 0)
);

CREATE TABLE dbo.Trips
(
    TripId BIGINT IDENTITY(1, 1) NOT NULL,
    TripCode NVARCHAR(20) NOT NULL,
    RouteScheduleVersionId BIGINT NOT NULL,
    RouteId BIGINT NOT NULL,
    ServiceDate DATE NOT NULL,
    ScheduledDepartureTime TIME(0) NOT NULL,
    ExpectedFinishLocal DATETIME2(0) NOT NULL,
    EstimatedDurationMinutesSnapshot INT NOT NULL,
    TripStatus NVARCHAR(30) NOT NULL,
    RequiresReview BIT NOT NULL
        CONSTRAINT DF_Trips_RequiresReview DEFAULT (0),
    OperationallyTouchedUtc DATETIME2(0) NULL,
    OperationallyTouchedByUserAccountId BIGINT NULL,
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Trips_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Trips_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Trips PRIMARY KEY CLUSTERED (TripId),
    CONSTRAINT FK_Trips_RouteScheduleVersions
        FOREIGN KEY (RouteScheduleVersionId, RouteId)
        REFERENCES dbo.RouteScheduleVersions (RouteScheduleVersionId, RouteId),
    CONSTRAINT FK_Trips_Routes
        FOREIGN KEY (RouteId) REFERENCES dbo.Routes (RouteId),
    CONSTRAINT FK_Trips_OperationallyTouchedByUserAccounts
        FOREIGN KEY (OperationallyTouchedByUserAccountId)
        REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_Trips_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_Trips_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_Trips_TripCode UNIQUE (TripCode),
    CONSTRAINT UQ_Trips_Route_ServiceDate_Departure
        UNIQUE (RouteId, ServiceDate, ScheduledDepartureTime),
    CONSTRAINT CK_Trips_TripCode_NotBlank
        CHECK (LEN(LTRIM(RTRIM(TripCode))) > 0),
    CONSTRAINT CK_Trips_EstimatedDurationMinutesSnapshot
        CHECK (EstimatedDurationMinutesSnapshot > 0),
    CONSTRAINT CK_Trips_TripStatus
        CHECK
        (
            TripStatus IN
            (
                N'Unassigned', N'Scheduled', N'Ready', N'InProgress',
                N'Delayed', N'Completed', N'Cancelled'
            )
        ),
    CONSTRAINT CK_Trips_OperationalTouch_Paired
        CHECK
        (
            (OperationallyTouchedUtc IS NULL AND OperationallyTouchedByUserAccountId IS NULL)
            OR (OperationallyTouchedUtc IS NOT NULL AND OperationallyTouchedByUserAccountId IS NOT NULL)
        ),
    CONSTRAINT CK_Trips_ExpectedFinish
        CHECK
        (
            ExpectedFinishLocal > DATETIME2FROMPARTS
            (
                DATEPART(YEAR, ServiceDate),
                DATEPART(MONTH, ServiceDate),
                DATEPART(DAY, ServiceDate),
                DATEPART(HOUR, ScheduledDepartureTime),
                DATEPART(MINUTE, ScheduledDepartureTime),
                DATEPART(SECOND, ScheduledDepartureTime),
                0,
                0
            )
        )
);

CREATE INDEX IX_Trips_ServiceDate_Route_Status
    ON dbo.Trips (ServiceDate, RouteId, TripStatus)
    INCLUDE (TripCode, ScheduledDepartureTime, ExpectedFinishLocal, RequiresReview);

CREATE INDEX IX_Trips_ScheduleVersion_ServiceDate
    ON dbo.Trips (RouteScheduleVersionId, ServiceDate, ScheduledDepartureTime);

CREATE INDEX IX_Trips_UpcomingUnassigned
    ON dbo.Trips (TripStatus, ServiceDate, ScheduledDepartureTime)
    INCLUDE (RouteId, RequiresReview);
