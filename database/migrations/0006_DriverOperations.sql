SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.PreTripInspections', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.TripExecutions', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.TripDelayEvents', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.TripCannotProceedReports', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.BusDefectReports', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.TripStatusHistory', N'U') IS NOT NULL
BEGIN
    THROW 51020, 'Driver-operation objects already exist but migration 0006 is not recorded.', 1;
END;

IF OBJECT_ID(N'dbo.Trips', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TripAssignments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.DriverProfiles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Buses', N'U') IS NULL
BEGIN
    THROW 51021, 'The scheduling, assignment, driver, or fleet foundation is missing.', 1;
END;

ALTER TABLE dbo.TripAssignments DROP CONSTRAINT CK_TripAssignments_EndType;
ALTER TABLE dbo.TripAssignments DROP CONSTRAINT CK_TripAssignments_CurrentEndState;

ALTER TABLE dbo.TripAssignments ADD CONSTRAINT CK_TripAssignments_EndType
    CHECK (EndType IS NULL OR EndType IN (N'Changed', N'Removed', N'Cancelled'));

ALTER TABLE dbo.TripAssignments ADD CONSTRAINT CK_TripAssignments_CurrentEndState
    CHECK
    (
        (IsCurrent = 1 AND EndType IS NULL AND EndReason IS NULL
            AND EndedByUserAccountId IS NULL AND EndedUtc IS NULL)
        OR
        (IsCurrent = 0 AND EndType IS NOT NULL
            AND EndReason IS NOT NULL AND LEN(LTRIM(RTRIM(EndReason))) > 0
            AND EndedByUserAccountId IS NOT NULL AND EndedUtc IS NOT NULL)
    );

ALTER TABLE dbo.TripAssignments ADD CONSTRAINT UQ_TripAssignments_OperationalContext
    UNIQUE (TripAssignmentId, TripId, DriverProfileId, BusId);

CREATE TABLE dbo.PreTripInspections
(
    PreTripInspectionId BIGINT IDENTITY(1, 1) NOT NULL,
    TripId BIGINT NOT NULL,
    TripAssignmentId BIGINT NOT NULL,
    DriverProfileId BIGINT NOT NULL,
    BusId BIGINT NOT NULL,
    ExteriorConditionChecked BIT NOT NULL,
    TyresSafeChecked BIT NOT NULL,
    LightsIndicatorsChecked BIT NOT NULL,
    NoCriticalDashboardWarningsChecked BIT NOT NULL,
    DoorsOperationalChecked BIT NOT NULL,
    EmergencyEquipmentPresentChecked BIT NOT NULL,
    NoBlockingNewDefectChecked BIT NOT NULL,
    StartOdometerKilometres DECIMAL(12, 1) NOT NULL,
    ConfirmedUtc DATETIME2(0) NOT NULL,
    InvalidatedUtc DATETIME2(0) NULL,
    InvalidatedByUserAccountId BIGINT NULL,
    InvalidationReason NVARCHAR(500) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_PreTripInspections PRIMARY KEY CLUSTERED (PreTripInspectionId),
    CONSTRAINT FK_PreTripInspections_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_PreTripInspections_AssignmentContext
        FOREIGN KEY (TripAssignmentId, TripId, DriverProfileId, BusId)
        REFERENCES dbo.TripAssignments (TripAssignmentId, TripId, DriverProfileId, BusId),
    CONSTRAINT FK_PreTripInspections_Buses FOREIGN KEY (BusId) REFERENCES dbo.Buses (BusId),
    CONSTRAINT FK_PreTripInspections_Drivers FOREIGN KEY (DriverProfileId) REFERENCES dbo.DriverProfiles (DriverProfileId),
    CONSTRAINT FK_PreTripInspections_InvalidatedBy FOREIGN KEY (InvalidatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_PreTripInspections_Assignment UNIQUE (TripAssignmentId),
    CONSTRAINT CK_PreTripInspections_ChecklistComplete CHECK
    (
        ExteriorConditionChecked = 1 AND TyresSafeChecked = 1
        AND LightsIndicatorsChecked = 1 AND NoCriticalDashboardWarningsChecked = 1
        AND DoorsOperationalChecked = 1 AND EmergencyEquipmentPresentChecked = 1
        AND NoBlockingNewDefectChecked = 1
    ),
    CONSTRAINT CK_PreTripInspections_StartOdometer CHECK (StartOdometerKilometres >= 0),
    CONSTRAINT CK_PreTripInspections_Invalidation CHECK
    (
        (InvalidatedUtc IS NULL AND InvalidatedByUserAccountId IS NULL AND InvalidationReason IS NULL)
        OR
        (InvalidatedUtc IS NOT NULL AND InvalidatedByUserAccountId IS NOT NULL
            AND InvalidationReason IS NOT NULL AND LEN(LTRIM(RTRIM(InvalidationReason))) > 0)
    )
);

CREATE UNIQUE INDEX UX_PreTripInspections_CurrentTrip
    ON dbo.PreTripInspections (TripId) WHERE InvalidatedUtc IS NULL;
CREATE INDEX IX_PreTripInspections_DriverConfirmed
    ON dbo.PreTripInspections (DriverProfileId, ConfirmedUtc DESC);

CREATE TABLE dbo.TripExecutions
(
    TripExecutionId BIGINT IDENTITY(1, 1) NOT NULL,
    TripId BIGINT NOT NULL,
    PreTripInspectionId BIGINT NOT NULL,
    TripAssignmentId BIGINT NOT NULL,
    DriverProfileId BIGINT NOT NULL,
    BusId BIGINT NOT NULL,
    ActualStartUtc DATETIME2(0) NOT NULL,
    ActualCompletionUtc DATETIME2(0) NULL,
    EndOdometerKilometres DECIMAL(12, 1) NULL,
    CompletionNote NVARCHAR(1000) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TripExecutions PRIMARY KEY CLUSTERED (TripExecutionId),
    CONSTRAINT UQ_TripExecutions_Trip UNIQUE (TripId),
    CONSTRAINT UQ_TripExecutions_Inspection UNIQUE (PreTripInspectionId),
    CONSTRAINT FK_TripExecutions_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_TripExecutions_Inspections FOREIGN KEY (PreTripInspectionId) REFERENCES dbo.PreTripInspections (PreTripInspectionId),
    CONSTRAINT FK_TripExecutions_AssignmentContext
        FOREIGN KEY (TripAssignmentId, TripId, DriverProfileId, BusId)
        REFERENCES dbo.TripAssignments (TripAssignmentId, TripId, DriverProfileId, BusId),
    CONSTRAINT FK_TripExecutions_Buses FOREIGN KEY (BusId) REFERENCES dbo.Buses (BusId),
    CONSTRAINT FK_TripExecutions_Drivers FOREIGN KEY (DriverProfileId) REFERENCES dbo.DriverProfiles (DriverProfileId),
    CONSTRAINT CK_TripExecutions_Completion CHECK
    (
        (ActualCompletionUtc IS NULL AND EndOdometerKilometres IS NULL AND CompletionNote IS NULL)
        OR
        (ActualCompletionUtc IS NOT NULL AND ActualCompletionUtc >= ActualStartUtc
            AND EndOdometerKilometres IS NOT NULL AND EndOdometerKilometres >= 0)
    )
);

CREATE INDEX IX_TripExecutions_DriverStart
    ON dbo.TripExecutions (DriverProfileId, ActualStartUtc DESC);
CREATE INDEX IX_TripExecutions_BusCompletion
    ON dbo.TripExecutions (BusId, ActualCompletionUtc DESC);

CREATE TABLE dbo.TripDelayEvents
(
    TripDelayEventId BIGINT IDENTITY(1, 1) NOT NULL,
    TripId BIGINT NOT NULL,
    TripAssignmentId BIGINT NOT NULL,
    DriverProfileId BIGINT NOT NULL,
    BusId BIGINT NOT NULL,
    DelayPhase NVARCHAR(20) NOT NULL,
    Reason NVARCHAR(500) NOT NULL,
    EstimatedDelayMinutes INT NULL,
    ReportedUtc DATETIME2(0) NOT NULL,
    EndedUtc DATETIME2(0) NULL,
    EndType NVARCHAR(30) NULL,
    EndedByUserAccountId BIGINT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TripDelayEvents PRIMARY KEY CLUSTERED (TripDelayEventId),
    CONSTRAINT FK_TripDelayEvents_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_TripDelayEvents_AssignmentContext
        FOREIGN KEY (TripAssignmentId, TripId, DriverProfileId, BusId)
        REFERENCES dbo.TripAssignments (TripAssignmentId, TripId, DriverProfileId, BusId),
    CONSTRAINT FK_TripDelayEvents_EndedBy FOREIGN KEY (EndedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_TripDelayEvents_Phase CHECK (DelayPhase IN (N'PreStart', N'InTrip')),
    CONSTRAINT CK_TripDelayEvents_Reason CHECK (LEN(LTRIM(RTRIM(Reason))) > 0),
    CONSTRAINT CK_TripDelayEvents_Estimate CHECK (EstimatedDelayMinutes IS NULL OR EstimatedDelayMinutes > 0),
    CONSTRAINT CK_TripDelayEvents_EndState CHECK
    (
        (EndedUtc IS NULL AND EndType IS NULL AND EndedByUserAccountId IS NULL)
        OR
        (EndedUtc IS NOT NULL AND EndType IN (N'Started', N'Resumed', N'Completed', N'AssignmentChanged', N'AssignmentRemoved', N'Cancelled')
            AND EndedByUserAccountId IS NOT NULL)
    )
);

CREATE UNIQUE INDEX UX_TripDelayEvents_OpenTrip
    ON dbo.TripDelayEvents (TripId) WHERE EndedUtc IS NULL;
CREATE INDEX IX_TripDelayEvents_TripHistory
    ON dbo.TripDelayEvents (TripId, ReportedUtc DESC);

CREATE TABLE dbo.TripCannotProceedReports
(
    TripCannotProceedReportId BIGINT IDENTITY(1, 1) NOT NULL,
    TripId BIGINT NOT NULL,
    TripAssignmentId BIGINT NOT NULL,
    DriverProfileId BIGINT NOT NULL,
    BusId BIGINT NOT NULL,
    OccurrencePhase NVARCHAR(20) NOT NULL,
    Reason NVARCHAR(500) NOT NULL,
    Note NVARCHAR(1000) NULL,
    ReportedUtc DATETIME2(0) NOT NULL,
    ResolutionType NVARCHAR(30) NULL,
    ResolutionNote NVARCHAR(1000) NULL,
    ResolvedByUserAccountId BIGINT NULL,
    ResolvedUtc DATETIME2(0) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TripCannotProceedReports PRIMARY KEY CLUSTERED (TripCannotProceedReportId),
    CONSTRAINT FK_TripCannotProceedReports_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_TripCannotProceedReports_AssignmentContext
        FOREIGN KEY (TripAssignmentId, TripId, DriverProfileId, BusId)
        REFERENCES dbo.TripAssignments (TripAssignmentId, TripId, DriverProfileId, BusId),
    CONSTRAINT FK_TripCannotProceedReports_ResolvedBy FOREIGN KEY (ResolvedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_TripCannotProceedReports_Phase CHECK (OccurrencePhase IN (N'PreStart', N'InTrip')),
    CONSTRAINT CK_TripCannotProceedReports_Reason CHECK (LEN(LTRIM(RTRIM(Reason))) > 0),
    CONSTRAINT CK_TripCannotProceedReports_Resolution CHECK
    (
        (ResolvedUtc IS NULL AND ResolutionType IS NULL AND ResolutionNote IS NULL AND ResolvedByUserAccountId IS NULL)
        OR
        (ResolvedUtc IS NOT NULL AND ResolutionType IN (N'Proceed', N'AssignmentChanged', N'AssignmentRemoved', N'Cancelled')
            AND ResolutionNote IS NOT NULL AND LEN(LTRIM(RTRIM(ResolutionNote))) > 0
            AND ResolvedByUserAccountId IS NOT NULL)
    )
);

CREATE UNIQUE INDEX UX_TripCannotProceedReports_OpenTrip
    ON dbo.TripCannotProceedReports (TripId) WHERE ResolvedUtc IS NULL;
CREATE INDEX IX_TripCannotProceedReports_AdminQueue
    ON dbo.TripCannotProceedReports (ResolvedUtc, ReportedUtc DESC) INCLUDE (TripId, BusId, DriverProfileId);

CREATE TABLE dbo.BusDefectReports
(
    BusDefectReportId BIGINT IDENTITY(1, 1) NOT NULL,
    DefectCode NVARCHAR(20) NOT NULL,
    BusId BIGINT NOT NULL,
    TripId BIGINT NULL,
    TripAssignmentId BIGINT NULL,
    ReportedByDriverProfileId BIGINT NOT NULL,
    Category NVARCHAR(30) NOT NULL,
    Severity NVARCHAR(20) NOT NULL,
    Description NVARCHAR(1000) NOT NULL,
    DefectStatus NVARCHAR(20) NOT NULL,
    ReportedUtc DATETIME2(0) NOT NULL,
    ReviewedByUserAccountId BIGINT NULL,
    ReviewedUtc DATETIME2(0) NULL,
    ReviewNote NVARCHAR(1000) NULL,
    ResolvedByUserAccountId BIGINT NULL,
    ResolvedUtc DATETIME2(0) NULL,
    ResolutionNote NVARCHAR(1000) NULL,
    UpdatedUtc DATETIME2(0) NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_BusDefectReports PRIMARY KEY CLUSTERED (BusDefectReportId),
    CONSTRAINT UQ_BusDefectReports_DefectCode UNIQUE (DefectCode),
    CONSTRAINT FK_BusDefectReports_Buses FOREIGN KEY (BusId) REFERENCES dbo.Buses (BusId),
    CONSTRAINT FK_BusDefectReports_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_BusDefectReports_Drivers FOREIGN KEY (ReportedByDriverProfileId) REFERENCES dbo.DriverProfiles (DriverProfileId),
    CONSTRAINT FK_BusDefectReports_AssignmentContext
        FOREIGN KEY (TripAssignmentId, TripId, ReportedByDriverProfileId, BusId)
        REFERENCES dbo.TripAssignments (TripAssignmentId, TripId, DriverProfileId, BusId),
    CONSTRAINT FK_BusDefectReports_ReviewedBy FOREIGN KEY (ReviewedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_BusDefectReports_ResolvedBy FOREIGN KEY (ResolvedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_BusDefectReports_Context CHECK
    (
        (TripId IS NULL AND TripAssignmentId IS NULL)
        OR (TripId IS NOT NULL AND TripAssignmentId IS NOT NULL)
    ),
    CONSTRAINT CK_BusDefectReports_Code CHECK (LEN(LTRIM(RTRIM(DefectCode))) > 0),
    CONSTRAINT CK_BusDefectReports_Category CHECK (Category IN (N'EngineDrivetrain', N'Brakes', N'Tyres', N'Electrical', N'Doors', N'Lights', N'BodyInterior', N'Other')),
    CONSTRAINT CK_BusDefectReports_Severity CHECK (Severity IN (N'Minor', N'Major', N'Critical')),
    CONSTRAINT CK_BusDefectReports_Status CHECK (DefectStatus IN (N'Open', N'Reviewed', N'Resolved')),
    CONSTRAINT CK_BusDefectReports_Description CHECK (LEN(LTRIM(RTRIM(Description))) > 0),
    CONSTRAINT CK_BusDefectReports_ReviewState CHECK
    (
        (DefectStatus = N'Open' AND ReviewedByUserAccountId IS NULL AND ReviewedUtc IS NULL AND ReviewNote IS NULL
            AND ResolvedByUserAccountId IS NULL AND ResolvedUtc IS NULL AND ResolutionNote IS NULL)
        OR
        (DefectStatus = N'Reviewed' AND ReviewedByUserAccountId IS NOT NULL AND ReviewedUtc IS NOT NULL
            AND ReviewNote IS NOT NULL AND LEN(LTRIM(RTRIM(ReviewNote))) > 0
            AND ResolvedByUserAccountId IS NULL AND ResolvedUtc IS NULL AND ResolutionNote IS NULL)
        OR
        (DefectStatus = N'Resolved' AND ResolvedByUserAccountId IS NOT NULL AND ResolvedUtc IS NOT NULL
            AND ResolutionNote IS NOT NULL AND LEN(LTRIM(RTRIM(ResolutionNote))) > 0)
    )
);

CREATE INDEX IX_BusDefectReports_AdminQueue
    ON dbo.BusDefectReports (DefectStatus, Severity, ReportedUtc DESC) INCLUDE (BusId, TripId, DefectCode);
CREATE INDEX IX_BusDefectReports_BusActive
    ON dbo.BusDefectReports (BusId, Severity, DefectStatus) INCLUDE (TripId, ReportedUtc);
CREATE INDEX IX_BusDefectReports_DriverHistory
    ON dbo.BusDefectReports (ReportedByDriverProfileId, ReportedUtc DESC);

CREATE TABLE dbo.TripStatusHistory
(
    TripStatusHistoryId BIGINT IDENTITY(1, 1) NOT NULL,
    TripId BIGINT NOT NULL,
    FromStatus NVARCHAR(30) NULL,
    ToStatus NVARCHAR(30) NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    OccurredUtc DATETIME2(0) NOT NULL,
    ActorUserAccountId BIGINT NOT NULL,
    Note NVARCHAR(1000) NULL,
    CONSTRAINT PK_TripStatusHistory PRIMARY KEY CLUSTERED (TripStatusHistoryId),
    CONSTRAINT FK_TripStatusHistory_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips (TripId),
    CONSTRAINT FK_TripStatusHistory_Actors FOREIGN KEY (ActorUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT CK_TripStatusHistory_FromStatus CHECK (FromStatus IS NULL OR FromStatus IN (N'Unassigned', N'Scheduled', N'Ready', N'InProgress', N'Delayed', N'Completed', N'Cancelled')),
    CONSTRAINT CK_TripStatusHistory_ToStatus CHECK (ToStatus IN (N'Unassigned', N'Scheduled', N'Ready', N'InProgress', N'Delayed', N'Completed', N'Cancelled')),
    CONSTRAINT CK_TripStatusHistory_EventType CHECK (EventType IN (N'AssignmentConfirmed', N'AssignmentChanged', N'AssignmentRemoved', N'TripReady', N'TripDelayReported', N'TripStarted', N'TripResumed', N'TripCompleted', N'TripCancelled'))
);

CREATE INDEX IX_TripStatusHistory_TripOccurred
    ON dbo.TripStatusHistory (TripId, OccurredUtc DESC, TripStatusHistoryId DESC);
