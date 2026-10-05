SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.FuelStations',N'U') IS NOT NULL
 OR OBJECT_ID(N'dbo.FuelStationCapabilities',N'U') IS NOT NULL
 OR OBJECT_ID(N'dbo.FuelRequests',N'U') IS NOT NULL
 OR OBJECT_ID(N'dbo.FuelVouchers',N'U') IS NOT NULL
 OR OBJECT_ID(N'dbo.FuelTransactions',N'U') IS NOT NULL
 THROW 51100, 'Fuel objects already exist but migration 0008 is not recorded.', 1;

CREATE TABLE dbo.FuelStations
(
 FuelStationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelStations PRIMARY KEY,
 StationCode NVARCHAR(20) NOT NULL CONSTRAINT UQ_FuelStations_Code UNIQUE,
 StationName NVARCHAR(150) NOT NULL,
 AreaDescription NVARCHAR(250) NOT NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_FuelStations_Active DEFAULT(1),
 CreatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 UpdatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CreatedUtc DATETIME2(0) NOT NULL,
 UpdatedUtc DATETIME2(0) NOT NULL,
 RowVersion ROWVERSION NOT NULL,
 CONSTRAINT CK_FuelStations_Text CHECK(LEN(LTRIM(RTRIM(StationName)))>0 AND LEN(LTRIM(RTRIM(AreaDescription)))>0)
);
CREATE TABLE dbo.FuelStationCapabilities
(
 FuelStationId BIGINT NOT NULL REFERENCES dbo.FuelStations(FuelStationId),
 FuelSupplyType NVARCHAR(30) NOT NULL,
 CONSTRAINT PK_FuelStationCapabilities PRIMARY KEY(FuelStationId,FuelSupplyType),
 CONSTRAINT CK_FuelStationCapabilities_Supply CHECK(FuelSupplyType IN(N'Diesel',N'ElectricCharging'))
);
CREATE TABLE dbo.FuelRequests
(
 FuelRequestId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelRequests PRIMARY KEY,
 RequestCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_FuelRequests_Code UNIQUE,
 TripAssignmentId BIGINT NOT NULL,
 TripId BIGINT NOT NULL,
 DriverProfileId BIGINT NOT NULL,
 BusId BIGINT NOT NULL,
 FuelSupplyType NVARCHAR(30) NOT NULL,
 Justification NVARCHAR(500) NOT NULL,
 OdometerAtRequest DECIMAL(12,1) NOT NULL,
 SubmissionToken UNIQUEIDENTIFIER NOT NULL CONSTRAINT UQ_FuelRequests_Submission UNIQUE,
 RequestStatus NVARCHAR(20) NOT NULL,
 SubmittedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 SubmittedUtc DATETIME2(0) NOT NULL,
 DecisionByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 DecisionUtc DATETIME2(0) NULL,
 DecisionNote NVARCHAR(500) NULL,
 CancelledByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CancelledUtc DATETIME2(0) NULL,
 CancellationReason NVARCHAR(500) NULL,
 UpdatedUtc DATETIME2(0) NOT NULL,
 RowVersion ROWVERSION NOT NULL,
 CONSTRAINT FK_FuelRequests_Context FOREIGN KEY(TripAssignmentId,TripId,DriverProfileId,BusId)
  REFERENCES dbo.TripAssignments(TripAssignmentId,TripId,DriverProfileId,BusId),
 CONSTRAINT FK_FuelRequests_Trip FOREIGN KEY(TripId) REFERENCES dbo.Trips(TripId),
 CONSTRAINT UQ_FuelRequests_Context UNIQUE(FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelSupplyType),
 CONSTRAINT CK_FuelRequests_Supply CHECK(FuelSupplyType IN(N'Diesel',N'ElectricCharging')),
 CONSTRAINT CK_FuelRequests_Values CHECK(LEN(LTRIM(RTRIM(Justification)))>0 AND OdometerAtRequest>=0),
 CONSTRAINT CK_FuelRequests_Status CHECK
 (
  (RequestStatus=N'Pending' AND DecisionByUserAccountId IS NULL AND DecisionUtc IS NULL AND DecisionNote IS NULL
   AND CancelledByUserAccountId IS NULL AND CancelledUtc IS NULL AND CancellationReason IS NULL)
  OR (RequestStatus IN(N'Approved',N'Rejected') AND DecisionByUserAccountId IS NOT NULL AND DecisionUtc IS NOT NULL
   AND (RequestStatus<>N'Rejected' OR (DecisionNote IS NOT NULL AND LEN(LTRIM(RTRIM(DecisionNote)))>0))
   AND CancelledByUserAccountId IS NULL AND CancelledUtc IS NULL AND CancellationReason IS NULL)
  OR (RequestStatus=N'Cancelled' AND DecisionByUserAccountId IS NULL AND DecisionUtc IS NULL AND DecisionNote IS NULL
   AND CancelledByUserAccountId IS NOT NULL AND CancelledUtc IS NOT NULL AND CancellationReason IS NOT NULL
   AND LEN(LTRIM(RTRIM(CancellationReason)))>0)
 )
);
CREATE UNIQUE INDEX UX_FuelRequests_PendingAssignment ON dbo.FuelRequests(TripAssignmentId) WHERE RequestStatus=N'Pending';
CREATE INDEX IX_FuelRequests_Queue ON dbo.FuelRequests(RequestStatus,SubmittedUtc);
CREATE INDEX IX_FuelRequests_Driver ON dbo.FuelRequests(DriverProfileId,SubmittedUtc);
CREATE INDEX IX_FuelRequests_Trip ON dbo.FuelRequests(TripId);

CREATE TABLE dbo.FuelVouchers
(
 FuelVoucherId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelVouchers PRIMARY KEY,
 VoucherCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_FuelVouchers_Code UNIQUE,
 FuelRequestId BIGINT NOT NULL CONSTRAINT UQ_FuelVouchers_Request UNIQUE,
 TripAssignmentId BIGINT NOT NULL,
 TripId BIGINT NOT NULL,
 DriverProfileId BIGINT NOT NULL,
 BusId BIGINT NOT NULL,
 FuelSupplyType NVARCHAR(30) NOT NULL,
 QuantityUnit NVARCHAR(20) NOT NULL,
 ApprovedQuantity DECIMAL(10,2) NOT NULL,
 ApprovedAmount DECIMAL(12,2) NOT NULL,
 FuelStationId BIGINT NOT NULL REFERENCES dbo.FuelStations(FuelStationId),
 StationCodeSnapshot NVARCHAR(20) NOT NULL,
 StationNameSnapshot NVARCHAR(150) NOT NULL,
 StationAreaSnapshot NVARCHAR(250) NOT NULL,
 FleetNumberSnapshot NVARCHAR(30) NOT NULL,
 ApprovedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 ApprovedUtc DATETIME2(0) NOT NULL,
 ApprovalNote NVARCHAR(500) NULL,
 ValidUntilLocal DATETIME2(0) NOT NULL,
 RedemptionTokenHash BINARY(32) NOT NULL CONSTRAINT UQ_FuelVouchers_TokenHash UNIQUE,
 ProtectedRedemptionToken VARBINARY(512) NOT NULL,
 VoucherStatus NVARCHAR(20) NOT NULL,
 RedeemedUtc DATETIME2(0) NULL,
 RedeemedByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CancelledUtc DATETIME2(0) NULL,
 CancelledByUserAccountId BIGINT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CancellationReason NVARCHAR(500) NULL,
 UpdatedUtc DATETIME2(0) NOT NULL,
 RowVersion ROWVERSION NOT NULL,
 CONSTRAINT FK_FuelVouchers_RequestContext FOREIGN KEY(FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelSupplyType)
  REFERENCES dbo.FuelRequests(FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelSupplyType),
 CONSTRAINT UQ_FuelVouchers_Context UNIQUE(FuelVoucherId,FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelStationId,FuelSupplyType),
 CONSTRAINT CK_FuelVouchers_Values CHECK(ApprovedQuantity>0 AND ApprovedAmount>=0 AND DATALENGTH(ProtectedRedemptionToken)>0),
 CONSTRAINT CK_FuelVouchers_Unit CHECK((FuelSupplyType=N'Diesel' AND QuantityUnit=N'Litres') OR (FuelSupplyType=N'ElectricCharging' AND QuantityUnit=N'KilowattHours')),
 CONSTRAINT CK_FuelVouchers_Status CHECK
 (
  (VoucherStatus=N'Active' AND RedeemedUtc IS NULL AND RedeemedByUserAccountId IS NULL AND CancelledUtc IS NULL AND CancelledByUserAccountId IS NULL AND CancellationReason IS NULL)
  OR (VoucherStatus=N'Redeemed' AND RedeemedUtc IS NOT NULL AND RedeemedByUserAccountId IS NOT NULL AND CancelledUtc IS NULL AND CancelledByUserAccountId IS NULL AND CancellationReason IS NULL)
  OR (VoucherStatus=N'Cancelled' AND RedeemedUtc IS NULL AND RedeemedByUserAccountId IS NULL AND CancelledUtc IS NOT NULL AND CancelledByUserAccountId IS NOT NULL AND CancellationReason IS NOT NULL AND LEN(LTRIM(RTRIM(CancellationReason)))>0)
 )
);
CREATE INDEX IX_FuelVouchers_Driver ON dbo.FuelVouchers(DriverProfileId,VoucherStatus,ValidUntilLocal);
CREATE INDEX IX_FuelVouchers_Assignment ON dbo.FuelVouchers(TripAssignmentId,VoucherStatus,ValidUntilLocal);
CREATE INDEX IX_FuelVouchers_Trip ON dbo.FuelVouchers(TripId,VoucherStatus);
CREATE INDEX IX_FuelVouchers_Station ON dbo.FuelVouchers(FuelStationId,VoucherStatus);

CREATE TABLE dbo.FuelTransactions
(
 FuelTransactionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelTransactions PRIMARY KEY,
 TransactionCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_FuelTransactions_Code UNIQUE,
 FuelVoucherId BIGINT NOT NULL CONSTRAINT UQ_FuelTransactions_Voucher UNIQUE,
 FuelRequestId BIGINT NOT NULL,
 TripAssignmentId BIGINT NOT NULL,
 TripId BIGINT NOT NULL,
 DriverProfileId BIGINT NOT NULL,
 BusId BIGINT NOT NULL,
 FuelStationId BIGINT NOT NULL,
 FuelSupplyType NVARCHAR(30) NOT NULL,
 QuantityUnit NVARCHAR(20) NOT NULL,
 Quantity DECIMAL(10,2) NOT NULL,
 Amount DECIMAL(12,2) NOT NULL,
 StationCodeSnapshot NVARCHAR(20) NOT NULL,
 StationNameSnapshot NVARCHAR(150) NOT NULL,
 StationAreaSnapshot NVARCHAR(250) NOT NULL,
 FleetNumberSnapshot NVARCHAR(30) NOT NULL,
 OdometerAtRedemption DECIMAL(12,1) NOT NULL,
 RedeemedUtc DATETIME2(0) NOT NULL,
 InitiatedByUserAccountId BIGINT NOT NULL REFERENCES dbo.UserAccounts(UserAccountId),
 CONSTRAINT FK_FuelTransactions_VoucherContext FOREIGN KEY(FuelVoucherId,FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelStationId,FuelSupplyType)
  REFERENCES dbo.FuelVouchers(FuelVoucherId,FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelStationId,FuelSupplyType),
 CONSTRAINT CK_FuelTransactions_Values CHECK(Quantity>0 AND Amount>=0 AND OdometerAtRedemption>=0),
 CONSTRAINT CK_FuelTransactions_Unit CHECK((FuelSupplyType=N'Diesel' AND QuantityUnit=N'Litres') OR (FuelSupplyType=N'ElectricCharging' AND QuantityUnit=N'KilowattHours'))
);
CREATE INDEX IX_FuelTransactions_Bus ON dbo.FuelTransactions(BusId,RedeemedUtc);
CREATE INDEX IX_FuelTransactions_Driver ON dbo.FuelTransactions(DriverProfileId,RedeemedUtc);
CREATE INDEX IX_FuelTransactions_Trip ON dbo.FuelTransactions(TripId,RedeemedUtc);
CREATE INDEX IX_FuelTransactions_Station ON dbo.FuelTransactions(FuelStationId,RedeemedUtc);
