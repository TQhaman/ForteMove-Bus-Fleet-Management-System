SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.MaintenancePlans',N'U') IS NOT NULL OR OBJECT_ID(N'dbo.RepairProviders',N'U') IS NOT NULL
 OR OBJECT_ID(N'dbo.MaintenanceWorkOrders',N'U') IS NOT NULL OR OBJECT_ID(N'dbo.MaintenanceProgressEntries',N'U') IS NOT NULL
 OR OBJECT_ID(N'dbo.BusVehicleStatusHistory',N'U') IS NOT NULL
 THROW 51200,'Unrecorded maintenance objects already exist. Do not overwrite this schema.',1;

CREATE TABLE dbo.RepairProviders(
 RepairProviderId BIGINT IDENTITY PRIMARY KEY,ProviderCode NVARCHAR(30) NOT NULL UNIQUE,
 ProviderName NVARCHAR(150) NOT NULL,AreaDescription NVARCHAR(250) NOT NULL,Phone NVARCHAR(30) NULL,Email NVARCHAR(254) NULL,
 IsActive BIT NOT NULL,CreatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 UpdatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),CreatedUtc DATETIME2(0) NOT NULL,UpdatedUtc DATETIME2(0) NOT NULL,RowVersion ROWVERSION,
 CONSTRAINT CK_RepairProviders_Text CHECK(LEN(LTRIM(RTRIM(ProviderName)))>0 AND LEN(LTRIM(RTRIM(AreaDescription)))>0));
CREATE TABLE dbo.MaintenancePlans(
 MaintenancePlanId BIGINT IDENTITY PRIMARY KEY,PlanCode NVARCHAR(30) NOT NULL UNIQUE,BusId BIGINT NOT NULL REFERENCES dbo.Buses(BusId),
 ServiceName NVARCHAR(150) NOT NULL,IntervalDays INT NULL,IntervalKilometres DECIMAL(12,1) NULL,
 LastServiceDate DATE NULL,LastServiceOdometer DECIMAL(12,1) NULL,NextDueDate DATE NULL,NextDueOdometer DECIMAL(12,1) NULL,
 BlocksOperationWhenOverdue BIT NOT NULL,IsActive BIT NOT NULL,LastCompletedWorkOrderId BIGINT NULL,
 CreatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),UpdatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CreatedUtc DATETIME2(0) NOT NULL,UpdatedUtc DATETIME2(0) NOT NULL,RowVersion ROWVERSION,
 CONSTRAINT UQ_MaintenancePlans_Context UNIQUE(MaintenancePlanId,BusId),
 CONSTRAINT CK_MaintenancePlans_Name CHECK(LEN(LTRIM(RTRIM(ServiceName)))>0),
 CONSTRAINT CK_MaintenancePlans_Intervals CHECK((IntervalDays IS NOT NULL OR IntervalKilometres IS NOT NULL) AND (IntervalDays IS NULL OR IntervalDays>0) AND (IntervalKilometres IS NULL OR IntervalKilometres>0)),
 CONSTRAINT CK_MaintenancePlans_Thresholds CHECK(((IntervalDays IS NULL AND NextDueDate IS NULL) OR (IntervalDays IS NOT NULL AND NextDueDate IS NOT NULL)) AND ((IntervalKilometres IS NULL AND NextDueOdometer IS NULL) OR (IntervalKilometres IS NOT NULL AND NextDueOdometer IS NOT NULL)) AND (LastServiceOdometer IS NULL OR LastServiceOdometer>=0) AND (NextDueOdometer IS NULL OR NextDueOdometer>=0)));
ALTER TABLE dbo.BusDefectReports ADD CONSTRAINT UQ_BusDefectReports_MaintenanceContext UNIQUE(BusDefectReportId,BusId);
ALTER TABLE dbo.TripCannotProceedReports ADD CONSTRAINT UQ_CannotProceed_MaintenanceContext UNIQUE(TripCannotProceedReportId,BusId);
CREATE TABLE dbo.MaintenanceWorkOrders(
 MaintenanceWorkOrderId BIGINT IDENTITY PRIMARY KEY,WorkOrderCode NVARCHAR(30) NOT NULL UNIQUE,CreationToken UNIQUEIDENTIFIER NOT NULL UNIQUE,
 BusId BIGINT NOT NULL REFERENCES dbo.Buses(BusId),RepairProviderId BIGINT NOT NULL REFERENCES dbo.RepairProviders(RepairProviderId),
 ProviderCodeSnapshot NVARCHAR(30) NOT NULL,ProviderNameSnapshot NVARCHAR(150) NOT NULL,
 MaintenanceType NVARCHAR(30) NOT NULL,TriggerType NVARCHAR(30) NOT NULL,BusDefectReportId BIGINT NULL,MaintenancePlanId BIGINT NULL,TripCannotProceedReportId BIGINT NULL,
 ComplianceRequirement NVARCHAR(30) NULL,RequestedWork NVARCHAR(1000) NOT NULL,TargetDate DATE NULL,OrderStatus NVARCHAR(20) NOT NULL,
 OpenedUtc DATETIME2(0) NOT NULL,StartedUtc DATETIME2(0) NULL,StartedByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CompletedUtc DATETIME2(0) NULL,CompletedByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CancelledUtc DATETIME2(0) NULL,CancelledByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),CancellationReason NVARCHAR(1000) NULL,
 ServiceOdometer DECIMAL(12,1) NULL,WorkPerformed NVARCHAR(1000) NULL,CompletionNote NVARCHAR(1000) NULL,ExternalReference NVARCHAR(100) NULL,CompletedCost DECIMAL(12,2) NULL,
 PlanNextDueDateSnapshot DATE NULL,PlanNextDueOdometerSnapshot DECIMAL(12,1) NULL,PlanIntervalDaysSnapshot INT NULL,PlanIntervalKilometresSnapshot DECIMAL(12,1) NULL,
 CreatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),UpdatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),UpdatedUtc DATETIME2(0) NOT NULL,RowVersion ROWVERSION,
 CONSTRAINT UQ_MaintenanceWorkOrders_Context UNIQUE(MaintenanceWorkOrderId,BusId),
 CONSTRAINT FK_MaintenanceWorkOrders_Defect FOREIGN KEY(BusDefectReportId,BusId) REFERENCES dbo.BusDefectReports(BusDefectReportId,BusId),
 CONSTRAINT FK_MaintenanceWorkOrders_Plan FOREIGN KEY(MaintenancePlanId,BusId) REFERENCES dbo.MaintenancePlans(MaintenancePlanId,BusId),
 CONSTRAINT FK_MaintenanceWorkOrders_Exception FOREIGN KEY(TripCannotProceedReportId,BusId) REFERENCES dbo.TripCannotProceedReports(TripCannotProceedReportId,BusId),
 CONSTRAINT CK_MaintenanceWorkOrders_Types CHECK(MaintenanceType IN(N'Preventive',N'Corrective',N'Breakdown') AND TriggerType IN(N'ServiceDate',N'ServiceOdometer',N'Defect',N'Breakdown',N'ComplianceExpiry',N'Manual')),
 CONSTRAINT CK_MaintenanceWorkOrders_Source CHECK((TriggerType NOT IN(N'ServiceDate',N'ServiceOdometer') OR MaintenancePlanId IS NOT NULL) AND (TriggerType<>N'Defect' OR BusDefectReportId IS NOT NULL) AND (TriggerType<>N'Breakdown' OR BusDefectReportId IS NOT NULL OR TripCannotProceedReportId IS NOT NULL) AND ((TriggerType=N'ComplianceExpiry' AND ComplianceRequirement IS NOT NULL AND ComplianceRequirement IN(N'Licence',N'Roadworthy',N'Insurance')) OR (TriggerType<>N'ComplianceExpiry' AND ComplianceRequirement IS NULL))),
 CONSTRAINT CK_MaintenanceWorkOrders_Text CHECK(LEN(LTRIM(RTRIM(RequestedWork)))>0),
 CONSTRAINT CK_MaintenanceWorkOrders_Values CHECK((ServiceOdometer IS NULL OR ServiceOdometer>=0) AND (CompletedCost IS NULL OR CompletedCost>=0)),
 CONSTRAINT CK_MaintenanceWorkOrders_Lifecycle CHECK(
 (OrderStatus=N'Open' AND StartedUtc IS NULL AND CompletedUtc IS NULL AND CancelledUtc IS NULL) OR
 (OrderStatus=N'InProgress' AND StartedUtc IS NOT NULL AND CompletedUtc IS NULL AND CancelledUtc IS NULL) OR
 (OrderStatus=N'Completed' AND StartedUtc IS NOT NULL AND CompletedUtc IS NOT NULL AND CancelledUtc IS NULL AND CompletedByUserAccountId IS NOT NULL AND ServiceOdometer IS NOT NULL AND WorkPerformed IS NOT NULL AND CompletionNote IS NOT NULL AND LEN(LTRIM(RTRIM(WorkPerformed)))>0 AND LEN(LTRIM(RTRIM(CompletionNote)))>0) OR
 (OrderStatus=N'Cancelled' AND CancelledUtc IS NOT NULL AND CancelledByUserAccountId IS NOT NULL AND CompletedUtc IS NULL AND CancellationReason IS NOT NULL AND LEN(LTRIM(RTRIM(CancellationReason)))>0)),
 CONSTRAINT CK_MaintenanceWorkOrders_CompletionMetadata CHECK(
 (CompletedUtc IS NULL AND CompletedByUserAccountId IS NULL AND ServiceOdometer IS NULL AND WorkPerformed IS NULL AND CompletionNote IS NULL AND CompletedCost IS NULL AND ExternalReference IS NULL) OR
 (CompletedUtc IS NOT NULL AND CompletedByUserAccountId IS NOT NULL AND ServiceOdometer IS NOT NULL AND WorkPerformed IS NOT NULL AND CompletionNote IS NOT NULL)),
 CONSTRAINT CK_MaintenanceWorkOrders_CancellationMetadata CHECK(
 (CancelledUtc IS NULL AND CancelledByUserAccountId IS NULL AND CancellationReason IS NULL) OR
 (CancelledUtc IS NOT NULL AND CancelledByUserAccountId IS NOT NULL AND CancellationReason IS NOT NULL)),
 CONSTRAINT CK_MaintenanceWorkOrders_Times CHECK((StartedUtc IS NULL OR StartedUtc>=OpenedUtc) AND (CompletedUtc IS NULL OR CompletedUtc>=StartedUtc) AND (CancelledUtc IS NULL OR CancelledUtc>=OpenedUtc) AND ((StartedUtc IS NULL AND StartedByUserAccountId IS NULL) OR (StartedUtc IS NOT NULL AND StartedByUserAccountId IS NOT NULL))));
ALTER TABLE dbo.MaintenancePlans ADD CONSTRAINT FK_MaintenancePlans_LastCompletion FOREIGN KEY(LastCompletedWorkOrderId,BusId) REFERENCES dbo.MaintenanceWorkOrders(MaintenanceWorkOrderId,BusId);
CREATE UNIQUE INDEX UX_MaintenanceWorkOrders_InProgressBus ON dbo.MaintenanceWorkOrders(BusId) WHERE OrderStatus=N'InProgress';
CREATE UNIQUE INDEX UX_MaintenanceWorkOrders_ActiveDefect ON dbo.MaintenanceWorkOrders(BusDefectReportId) WHERE BusDefectReportId IS NOT NULL AND OrderStatus IN(N'Open',N'InProgress');
CREATE UNIQUE INDEX UX_MaintenanceWorkOrders_ActivePlan ON dbo.MaintenanceWorkOrders(MaintenancePlanId) WHERE MaintenancePlanId IS NOT NULL AND OrderStatus IN(N'Open',N'InProgress');
CREATE UNIQUE INDEX UX_MaintenanceWorkOrders_ActiveException ON dbo.MaintenanceWorkOrders(TripCannotProceedReportId) WHERE TripCannotProceedReportId IS NOT NULL AND OrderStatus IN(N'Open',N'InProgress');
CREATE INDEX IX_MaintenancePlans_Attention ON dbo.MaintenancePlans(BusId,IsActive,BlocksOperationWhenOverdue) INCLUDE(NextDueDate,NextDueOdometer);
CREATE INDEX IX_MaintenanceWorkOrders_BusHistory ON dbo.MaintenanceWorkOrders(BusId,OpenedUtc DESC);
CREATE INDEX IX_MaintenanceWorkOrders_Queue ON dbo.MaintenanceWorkOrders(OrderStatus,TargetDate);
CREATE TABLE dbo.MaintenanceProgressEntries(
 MaintenanceProgressEntryId BIGINT IDENTITY PRIMARY KEY,MaintenanceWorkOrderId BIGINT NOT NULL REFERENCES dbo.MaintenanceWorkOrders(MaintenanceWorkOrderId),
 Note NVARCHAR(1000) NOT NULL,RecordedUtc DATETIME2(0) NOT NULL,RecordedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CONSTRAINT CK_MaintenanceProgressEntries_Note CHECK(LEN(LTRIM(RTRIM(Note)))>0));
CREATE INDEX IX_MaintenanceProgressEntries_History ON dbo.MaintenanceProgressEntries(MaintenanceWorkOrderId,RecordedUtc);
CREATE TABLE dbo.BusVehicleStatusHistory(
 BusVehicleStatusHistoryId BIGINT IDENTITY PRIMARY KEY,BusId BIGINT NOT NULL REFERENCES dbo.Buses(BusId),
 FromStatus NVARCHAR(30) NOT NULL,ToStatus NVARCHAR(30) NOT NULL,Reason NVARCHAR(1000) NOT NULL,
 MaintenanceWorkOrderId BIGINT NULL,OccurredUtc DATETIME2(0) NOT NULL,ActorUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CONSTRAINT FK_BusVehicleStatusHistory_Order FOREIGN KEY(MaintenanceWorkOrderId,BusId) REFERENCES dbo.MaintenanceWorkOrders(MaintenanceWorkOrderId,BusId),
 CONSTRAINT CK_BusVehicleStatusHistory_Transition CHECK(FromStatus<>ToStatus AND FromStatus IN(N'Operational',N'OutOfService',N'UnderMaintenance',N'Retired') AND ToStatus IN(N'Operational',N'OutOfService',N'UnderMaintenance',N'Retired') AND LEN(LTRIM(RTRIM(Reason)))>0));
CREATE INDEX IX_BusVehicleStatusHistory_Bus ON dbo.BusVehicleStatusHistory(BusId,OccurredUtc);
