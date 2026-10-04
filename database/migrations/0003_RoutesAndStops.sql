SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Routes', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.Stops', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.RouteStops', N'U') IS NOT NULL
BEGIN
    THROW 51003, 'Route and stop objects already exist but migration 0003 is not recorded.', 1;
END;

IF OBJECT_ID(N'dbo.Buses', N'U') IS NULL
BEGIN
    THROW 51004, 'The fleet foundation is missing. Migration 0003 cannot continue.', 1;
END;

IF COL_LENGTH(N'dbo.Buses', N'VinChassisNumber') IS NULL
   OR COL_LENGTH(N'dbo.Buses', N'Vin') IS NOT NULL
   OR OBJECT_ID(N'dbo.UQ_Buses_VinChassisNumber', N'UQ') IS NULL
   OR OBJECT_ID(N'dbo.UQ_Buses_Vin', N'UQ') IS NOT NULL
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes AS index_definition
       WHERE index_definition.object_id = OBJECT_ID(N'dbo.Buses', N'U')
         AND index_definition.name = N'UQ_Buses_VinChassisNumber'
         AND index_definition.is_unique = 1
   )
   OR EXISTS
   (
       SELECT 1
       FROM sys.indexes AS index_definition
       WHERE index_definition.object_id = OBJECT_ID(N'dbo.Buses', N'U')
         AND index_definition.name = N'UQ_Buses_Vin'
   )
BEGIN
    THROW 51005, 'The Buses VIN schema does not match the expected migration 0002 state.', 1;
END;

EXEC sys.sp_rename
    @objname = N'dbo.Buses.VinChassisNumber',
    @newname = N'Vin',
    @objtype = N'COLUMN';

EXEC sys.sp_rename
    @objname = N'dbo.UQ_Buses_VinChassisNumber',
    @newname = N'UQ_Buses_Vin',
    @objtype = N'OBJECT';

IF COL_LENGTH(N'dbo.Buses', N'VinChassisNumber') IS NOT NULL
   OR COL_LENGTH(N'dbo.Buses', N'Vin') IS NULL
   OR OBJECT_ID(N'dbo.UQ_Buses_VinChassisNumber', N'UQ') IS NOT NULL
   OR OBJECT_ID(N'dbo.UQ_Buses_Vin', N'UQ') IS NULL
   OR EXISTS
   (
       SELECT 1
       FROM sys.indexes AS index_definition
       WHERE index_definition.object_id = OBJECT_ID(N'dbo.Buses', N'U')
         AND index_definition.name = N'UQ_Buses_VinChassisNumber'
   )
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes AS index_definition
       WHERE index_definition.object_id = OBJECT_ID(N'dbo.Buses', N'U')
         AND index_definition.name = N'UQ_Buses_Vin'
         AND index_definition.is_unique = 1
   )
BEGIN
    THROW 51006, 'The Buses VIN schema could not be renamed safely.', 1;
END;

CREATE TABLE dbo.Routes
(
    RouteId BIGINT IDENTITY(1, 1) NOT NULL,
    RouteCode NVARCHAR(20) NOT NULL,
    RouteName NVARCHAR(150) NOT NULL,
    EstimatedDistanceKm DECIMAL(8, 2) NOT NULL,
    EstimatedDurationMinutes INT NOT NULL,
    DefaultFare DECIMAL(10, 2) NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_Routes_IsActive DEFAULT (1),
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Routes_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Routes_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Routes PRIMARY KEY CLUSTERED (RouteId),
    CONSTRAINT FK_Routes_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_Routes_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_Routes_RouteCode UNIQUE (RouteCode),
    CONSTRAINT CK_Routes_RouteCode_NotBlank
        CHECK (LEN(LTRIM(RTRIM(RouteCode))) > 0),
    CONSTRAINT CK_Routes_RouteName_NotBlank
        CHECK (LEN(LTRIM(RTRIM(RouteName))) > 0),
    CONSTRAINT CK_Routes_EstimatedDistanceKm
        CHECK (EstimatedDistanceKm > 0),
    CONSTRAINT CK_Routes_EstimatedDurationMinutes
        CHECK (EstimatedDurationMinutes > 0),
    CONSTRAINT CK_Routes_DefaultFare
        CHECK (DefaultFare >= 0)
);

CREATE INDEX IX_Routes_IsActive_RouteName
    ON dbo.Routes (IsActive, RouteName);

CREATE TABLE dbo.Stops
(
    StopId BIGINT IDENTITY(1, 1) NOT NULL,
    StopCode NVARCHAR(20) NOT NULL,
    StopName NVARCHAR(150) NOT NULL,
    NormalizedStopName NVARCHAR(150) NOT NULL,
    Area NVARCHAR(150) NOT NULL,
    NormalizedArea NVARCHAR(150) NOT NULL,
    Latitude DECIMAL(9, 6) NULL,
    Longitude DECIMAL(9, 6) NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_Stops_IsActive DEFAULT (1),
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Stops_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Stops_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Stops PRIMARY KEY CLUSTERED (StopId),
    CONSTRAINT FK_Stops_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_Stops_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_Stops_StopCode UNIQUE (StopCode),
    CONSTRAINT CK_Stops_StopCode_NotBlank
        CHECK (LEN(LTRIM(RTRIM(StopCode))) > 0),
    CONSTRAINT CK_Stops_StopName_NotBlank
        CHECK (LEN(LTRIM(RTRIM(StopName))) > 0),
    CONSTRAINT CK_Stops_NormalizedStopName_NotBlank
        CHECK (LEN(LTRIM(RTRIM(NormalizedStopName))) > 0),
    CONSTRAINT CK_Stops_Area_NotBlank
        CHECK (LEN(LTRIM(RTRIM(Area))) > 0),
    CONSTRAINT CK_Stops_NormalizedArea_NotBlank
        CHECK (LEN(LTRIM(RTRIM(NormalizedArea))) > 0),
    CONSTRAINT CK_Stops_Coordinates_Paired
        CHECK
        (
            (Latitude IS NULL AND Longitude IS NULL)
            OR (Latitude IS NOT NULL AND Longitude IS NOT NULL)
        ),
    CONSTRAINT CK_Stops_Latitude
        CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
    CONSTRAINT CK_Stops_Longitude
        CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
);

CREATE UNIQUE INDEX UX_Stops_ActiveNormalizedNameArea
    ON dbo.Stops (NormalizedStopName, NormalizedArea)
    WHERE IsActive = 1;

CREATE INDEX IX_Stops_IsActive_StopName_Area
    ON dbo.Stops (IsActive, StopName, Area);

CREATE TABLE dbo.RouteStops
(
    RouteStopId BIGINT IDENTITY(1, 1) NOT NULL,
    RouteId BIGINT NOT NULL,
    StopId BIGINT NOT NULL,
    StopOrder INT NOT NULL,
    EstimatedMinutesFromOrigin INT NULL,
    CONSTRAINT PK_RouteStops PRIMARY KEY CLUSTERED (RouteStopId),
    CONSTRAINT FK_RouteStops_Routes
        FOREIGN KEY (RouteId) REFERENCES dbo.Routes (RouteId),
    CONSTRAINT FK_RouteStops_Stops
        FOREIGN KEY (StopId) REFERENCES dbo.Stops (StopId),
    CONSTRAINT UQ_RouteStops_Route_StopOrder
        UNIQUE (RouteId, StopOrder),
    CONSTRAINT UQ_RouteStops_Route_Stop
        UNIQUE (RouteId, StopId),
    CONSTRAINT CK_RouteStops_StopOrder
        CHECK (StopOrder >= 1),
    CONSTRAINT CK_RouteStops_EstimatedMinutesFromOrigin
        CHECK (EstimatedMinutesFromOrigin IS NULL OR EstimatedMinutesFromOrigin >= 0)
);

CREATE INDEX IX_RouteStops_StopId_RouteId
    ON dbo.RouteStops (StopId, RouteId);
