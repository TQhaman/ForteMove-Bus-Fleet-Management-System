SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigrations
    (
        MigrationName NVARCHAR(260) NOT NULL,
        ChecksumSha256 CHAR(64) NOT NULL,
        AppliedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_SchemaMigrations_AppliedUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SchemaMigrations PRIMARY KEY CLUSTERED (MigrationName),
        CONSTRAINT CK_SchemaMigrations_ChecksumSha256_Length
            CHECK (LEN(ChecksumSha256) = 64)
    );
END;
