SET NOCOUNT ON;
SET XACT_ABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @RoleId INT = (SELECT RoleId FROM dbo.Roles WHERE RoleCode=N'Passenger');
    DECLARE @TripId BIGINT = (SELECT MIN(TripId) FROM dbo.Trips);
    IF @RoleId IS NULL OR @TripId IS NULL THROW 51100, 'Verification prerequisites are unavailable.', 1;

    INSERT dbo.UserAccounts
        (RoleId,Email,NormalizedEmail,PasswordAlgorithm,PasswordHash,PasswordSalt,
         PasswordIterations,IsActive,MustChangePassword,FailedLoginCount,CreatedUtc,UpdatedUtc)
    VALUES
        (@RoleId,N'slice6.rollback@example.test',N'SLICE6.ROLLBACK@EXAMPLE.TEST',
         N'PBKDF2-HMAC-SHA256',CONVERT(varbinary(32),REPLICATE('H',32)),
         CONVERT(varbinary(32),REPLICATE('S',32)),600000,1,0,0,SYSUTCDATETIME(),SYSUTCDATETIME());
    DECLARE @UserId BIGINT=SCOPE_IDENTITY();

    INSERT dbo.PassengerProfiles(UserAccountId,FirstName,LastName,CreatedUtc,UpdatedUtc)
    VALUES(@UserId,N'Rollback',N'Passenger',SYSUTCDATETIME(),SYSUTCDATETIME());
    DECLARE @ProfileId BIGINT=SCOPE_IDENTITY();

    INSERT dbo.PassengerWallets(PassengerProfileId,CurrentBalance,CreatedUtc,UpdatedUtc)
    VALUES(@ProfileId,10.00,SYSUTCDATETIME(),SYSUTCDATETIME());
    DECLARE @WalletId BIGINT=SCOPE_IDENTITY();

    INSERT dbo.Tickets(TicketCode,PassengerProfileId,TripId,FareAmount,TicketStatus,PurchasedUtc,UpdatedUtc)
    VALUES(N'TKT-ROLLBACK',@ProfileId,@TripId,0.00,N'Purchased',SYSUTCDATETIME(),SYSUTCDATETIME());
    DECLARE @TicketId BIGINT=SCOPE_IDENTITY();

    INSERT dbo.WalletTransactions
        (WalletTransactionCode,PassengerWalletId,TransactionType,Amount,BalanceBefore,
         BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
    VALUES
        (N'WTX-ROLLBACK-1',@WalletId,N'TicketPurchase',0.00,10.00,10.00,
         @TicketId,@UserId,NEWID(),SYSUTCDATETIME());

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.Tickets t
        JOIN dbo.WalletTransactions x ON x.TicketId=t.TicketId
        WHERE t.TicketId=@TicketId AND t.FareAmount=0 AND x.Amount=0
          AND x.BalanceBefore=x.BalanceAfter
    ) THROW 51101, 'R0.00 Ticket verification failed.', 1;

    BEGIN TRY
        INSERT dbo.Tickets(TicketCode,PassengerProfileId,TripId,FareAmount,TicketStatus,PurchasedUtc,UpdatedUtc)
        VALUES(N'TKT-ROLLBACK-DUP',@ProfileId,@TripId,0.00,N'Purchased',SYSUTCDATETIME(),SYSUTCDATETIME());
        THROW 51102, 'Duplicate purchased Ticket was not rejected.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
    END CATCH;

    BEGIN TRY
        INSERT dbo.WalletTransactions
            (WalletTransactionCode,PassengerWalletId,TransactionType,Amount,BalanceBefore,
             BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
        VALUES
            (N'WTX-ROLLBACK-BAD',@WalletId,N'SimulatedTopUp',1.00,10.00,10.50,
             NULL,@UserId,NEWID(),SYSUTCDATETIME());
        THROW 51103, 'Invalid wallet arithmetic was not rejected.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;

    UPDATE dbo.Tickets
    SET TicketStatus=N'Refunded',RefundedUtc=SYSUTCDATETIME(),
        RefundedByUserAccountId=@UserId,RefundReason=N'Rollback cancellation test',
        UpdatedUtc=SYSUTCDATETIME()
    WHERE TicketId=@TicketId;

    INSERT dbo.WalletTransactions
        (WalletTransactionCode,PassengerWalletId,TransactionType,Amount,BalanceBefore,
         BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
    VALUES
        (N'WTX-ROLLBACK-2',@WalletId,N'TripCancellationRefund',0.00,10.00,10.00,
         @TicketId,@UserId,NEWID(),SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM dbo.Tickets WHERE TicketId=@TicketId AND TicketStatus=N'Refunded')
        THROW 51104, 'Refunded Ticket history was not retained.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Tickets WHERE TripId=@TripId)
        THROW 51105, 'Ticket history did not protect the Trip dependency.', 1;

    SELECT N'PASS' AS Result,
           N'R0 Ticket, ledger arithmetic, duplicate purchase, refund history, rollback' AS Verification;
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.PassengerProfiles) AS PassengerProfiles,
    (SELECT COUNT_BIG(*) FROM dbo.PassengerWallets) AS PassengerWallets,
    (SELECT COUNT_BIG(*) FROM dbo.Tickets) AS Tickets,
    (SELECT COUNT_BIG(*) FROM dbo.WalletTransactions) AS WalletTransactions;
