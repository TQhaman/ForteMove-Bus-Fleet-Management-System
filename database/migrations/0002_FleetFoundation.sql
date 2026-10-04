SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.BusCategories', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.PropulsionTypes', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.Buses', N'U') IS NOT NULL
BEGIN
    THROW 51002, 'Fleet foundation objects already exist but migration 0002 is not recorded.', 1;
END;

CREATE TABLE dbo.BusCategories
(
    BusCategoryId INT NOT NULL,
    CategoryCode NVARCHAR(50) NOT NULL,
    DisplayName NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_BusCategories_IsActive DEFAULT (1),
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_BusCategories_CreatedUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BusCategories PRIMARY KEY CLUSTERED (BusCategoryId),
    CONSTRAINT UQ_BusCategories_CategoryCode UNIQUE (CategoryCode)
);

CREATE TABLE dbo.PropulsionTypes
(
    PropulsionTypeId INT NOT NULL,
    PropulsionCode NVARCHAR(50) NOT NULL,
    DisplayName NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_PropulsionTypes_IsActive DEFAULT (1),
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_PropulsionTypes_CreatedUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PropulsionTypes PRIMARY KEY CLUSTERED (PropulsionTypeId),
    CONSTRAINT UQ_PropulsionTypes_PropulsionCode UNIQUE (PropulsionCode)
);

CREATE TABLE dbo.Buses
(
    BusId BIGINT IDENTITY(1, 1) NOT NULL,
    BusCategoryId INT NOT NULL,
    PropulsionTypeId INT NOT NULL,
    FleetNumber NVARCHAR(30) NOT NULL,
    RegistrationNumber NVARCHAR(30) NOT NULL,
    VinChassisNumber NVARCHAR(50) NOT NULL,
    Make NVARCHAR(100) NOT NULL,
    Model NVARCHAR(100) NOT NULL,
    ManufactureYear SMALLINT NOT NULL,
    PassengerCapacity SMALLINT NOT NULL,
    FuelTankCapacityLitres DECIMAL(10, 2) NULL,
    BatteryCapacityKwh DECIMAL(10, 2) NULL,
    OdometerKilometres DECIMAL(12, 1) NOT NULL,
    LicenceExpiryDate DATE NOT NULL,
    RoadworthyExpiryDate DATE NOT NULL,
    InsuranceExpiryDate DATE NOT NULL,
    BaseOperationalState NVARCHAR(30) NOT NULL,
    CreatedByUserAccountId BIGINT NOT NULL,
    UpdatedByUserAccountId BIGINT NOT NULL,
    CreatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Buses_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(0) NOT NULL
        CONSTRAINT DF_Buses_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Buses PRIMARY KEY CLUSTERED (BusId),
    CONSTRAINT FK_Buses_BusCategories
        FOREIGN KEY (BusCategoryId) REFERENCES dbo.BusCategories (BusCategoryId),
    CONSTRAINT FK_Buses_PropulsionTypes
        FOREIGN KEY (PropulsionTypeId) REFERENCES dbo.PropulsionTypes (PropulsionTypeId),
    CONSTRAINT FK_Buses_CreatedByUserAccounts
        FOREIGN KEY (CreatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT FK_Buses_UpdatedByUserAccounts
        FOREIGN KEY (UpdatedByUserAccountId) REFERENCES dbo.UserAccounts (UserAccountId),
    CONSTRAINT UQ_Buses_FleetNumber UNIQUE (FleetNumber),
    CONSTRAINT UQ_Buses_RegistrationNumber UNIQUE (RegistrationNumber),
    CONSTRAINT UQ_Buses_VinChassisNumber UNIQUE (VinChassisNumber),
    CONSTRAINT CK_Buses_ManufactureYear
        CHECK (ManufactureYear BETWEEN 1886 AND 2200),
    CONSTRAINT CK_Buses_PassengerCapacity
        CHECK (PassengerCapacity BETWEEN 1 AND 200),
    CONSTRAINT CK_Buses_FuelTankCapacityLitres
        CHECK (FuelTankCapacityLitres IS NULL OR FuelTankCapacityLitres > 0),
    CONSTRAINT CK_Buses_BatteryCapacityKwh
        CHECK (BatteryCapacityKwh IS NULL OR BatteryCapacityKwh > 0),
    CONSTRAINT CK_Buses_OdometerKilometres
        CHECK (OdometerKilometres >= 0),
    CONSTRAINT CK_Buses_BaseOperationalState
        CHECK (BaseOperationalState IN
            (N'Operational', N'OutOfService', N'UnderMaintenance', N'Retired'))
);

CREATE INDEX IX_Buses_BaseOperationalState
    ON dbo.Buses (BaseOperationalState);

CREATE INDEX IX_Buses_BusCategoryId
    ON dbo.Buses (BusCategoryId);

CREATE INDEX IX_Buses_PropulsionTypeId
    ON dbo.Buses (PropulsionTypeId);

INSERT dbo.BusCategories (BusCategoryId, CategoryCode, DisplayName, IsActive)
VALUES
    (1, N'Minibus', N'Minibus', 1),
    (2, N'Midibus', N'Midibus', 1),
    (3, N'StandardBus', N'Standard Bus', 1);

INSERT dbo.PropulsionTypes (PropulsionTypeId, PropulsionCode, DisplayName, IsActive)
VALUES
    (1, N'Petrol', N'Petrol', 1),
    (2, N'Diesel', N'Diesel', 1),
    (3, N'Hybrid', N'Hybrid', 1),
    (4, N'Electric', N'Electric', 1);
