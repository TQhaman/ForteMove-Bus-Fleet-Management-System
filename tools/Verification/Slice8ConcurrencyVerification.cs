using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using ForteMove.Business.Fuel;
using ForteMove.Business.Exceptions;
using ForteMove.Data.Repositories;
using ForteMove.Models.Fuel;
using ForteMove.Models.Assignments;
using ForteMove.Models.Operations;

internal static class Slice8ConcurrencyVerification
{
    static string Connection;static int checks;
    static DateTime Utc=DateTime.UtcNow, Today=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time")).Date;
    static SqlFuelRepository Repo(){return new SqlFuelRepository(Connection);}
    static SqlFuelStationRepository Stations(){return new SqlFuelStationRepository(Connection);}
    static SqlTripOperationsRepository Operations(){return new SqlTripOperationsRepository(Connection);}
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length!=1 || !args[0].StartsWith("ForteMove_Slice8_Verification_") || args[0].Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))
                throw new Exception("Use only a disposable ForteMove_Slice8_Verification_* database initialized with the migrations.");
            Connection=@"Data Source=.\SQLEXPRESS;Integrated Security=True;Initial Catalog="+args[0];
            Check(Number("SELECT COUNT(*) FROM dbo.UserAccounts;")==0,"Disposable database starts empty");
            Execute(Fixture);
            Stations().Save(new SaveFuelStationRequest{StationName="Synthetic station",AreaDescription="Disposable verification only",IsActive=true,SupportsDiesel=true,SupportsElectricCharging=true},1,Utc);
            // Duplicate submission is tested with different tokens: exactly one new Pending request may persist.
            var context=Repo().GetDriverTrip(2,2);var a=Submit(context);var b=Submit(context);
            var submissions=Race(()=>Repo().Submit(a,2,Now(2),Utc),()=>Repo().Submit(b,2,Now(2),Utc));
            Check(submissions.Count(x=>x.Error==null)==1 && submissions.Count(x=>x.Error is FuelPersistenceException)==1,"Concurrent Pending requests: one success, one friendly rejection");
            Check(Number("SELECT COUNT(*) FROM dbo.FuelRequests WHERE TripId=2 AND RequestStatus=N'Pending';")==1,"Filtered Pending constraint");
            var pending=Repo().GetRequests(new FuelQuery(),1,false).Single(x=>x.Context.TripId==2);
            Repo().Reject(new RejectFuelRequest{FuelRequestId=pending.FuelRequestId,Reason="Synthetic rejection",RowVersion=pending.RowVersion},1,Utc);
            Check(Repo().GetRequest(pending.FuelRequestId,1,false).Status==FuelRequestStatus.Rejected,"Production rejection persists reason");
            Check(Repo().Submit(Submit(Repo().GetDriverTrip(2,2)),2,Now(2),Utc).FuelRequestId!=pending.FuelRequestId,"New request after rejection allowed");
            // Concurrent approval of one Pending request creates exactly one Voucher.
            long request=Repo().Submit(Submit(Repo().GetDriverTrip(1,2)),2,Now(1),Utc).FuelRequestId;
            var approval=Approval(request);
            string t1=FuelRedemptionToken.Create(),t2=FuelRedemptionToken.Create();
            var approvals=Race(()=>Repo().Approve(approval,1,FuelRedemptionToken.Hash(t1),Encoding.UTF8.GetBytes(t1),Now(1),Utc),
                               ()=>Repo().Approve(approval,1,FuelRedemptionToken.Hash(t2),Encoding.UTF8.GetBytes(t2),Now(1),Utc));
            Check(approvals.Count(x=>x.Error==null)==1 && approvals.Count(x=>x.Error is FuelPersistenceException)==1,"Concurrent approval one winner");
            long voucherId=(long)approvals.Single(x=>x.Error==null).Value;
            string token=approvals[0].Error==null?t1:t2;
            Check(Number("SELECT COUNT(*) FROM dbo.FuelVouchers WHERE FuelRequestId="+request+";")==1,"One Voucher per approval");
            var voucher=Repo().GetVoucher(voucherId,1,false);var redemption=Redemption(voucher,token);
            var redemptions=Race(()=>Repo().Redeem(redemption,FuelRedemptionToken.Hash(token),1,Now(1),Utc),()=>Repo().Redeem(redemption,FuelRedemptionToken.Hash(token),1,Now(1),Utc));
            Check(redemptions.All(x=>x.Error==null),"Concurrent replay returns existing transaction without exception");
            Check(redemptions.Count(x=>!((FuelRedemptionResult)x.Value).AlreadyRedeemed)==1,"Exactly one redemption consumes Voucher");
            Check(redemptions.Select(x=>((FuelRedemptionResult)x.Value).FuelTransactionId).Distinct().Count()==1,"Both confirmations refer to same transaction");
            Check(Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE FuelVoucherId="+voucherId+";")==1,"One committed FuelTransaction");
            // Stale replacement must roll back old assignment closure and Fuel invalidation together.
            var third=CreateVoucher(3);var old=Repo().GetDriverTrip(3,2);
            var assignments=new SqlAssignmentRepository(Connection);
            var data=assignments.GetAssignmentData(old.ServiceDate);var driver=data.Drivers.Single(x=>x.DriverProfileId==old.DriverProfileId);var bus=data.Buses.Single(x=>x.BusId==old.BusId);
            var replacement=new ConfirmAssignmentItem{TripId=3,BusId=old.BusId,DriverProfileId=old.DriverProfileId,DecisionType=AssignmentDecisionType.Change,Reason="Synthetic change",
                TripRowVersion=old.TripRowVersion,BusRowVersion=new byte[8],DriverRowVersion=driver.DriverRowVersion,StaffRowVersion=driver.StaffRowVersion,UserRowVersion=driver.UserRowVersion};
            bool failed=false;try{assignments.ChangeAssignment(replacement,old.AssignmentRowVersion,1,Now(3),Utc);}catch(AssignmentPersistenceException){failed=true;}
            Check(failed && Repo().GetVoucher(third.Id,1,false).Status==FuelVoucherStatus.Active && Repo().GetDriverTrip(3,2).TripAssignmentId==old.TripAssignmentId,"Failed change rolls back Fuel invalidation and assignment");
            replacement.BusRowVersion=bus.RowVersion;
            assignments.ChangeAssignment(replacement,old.AssignmentRowVersion,1,Now(3),Utc);
            Check(Repo().GetVoucher(third.Id,1,false).Status==FuelVoucherStatus.Cancelled,"Real assignment change invalidates Voucher atomically");
            Check(Repo().GetDriverTrip(3,2).TripAssignmentId!=old.TripAssignmentId,"New assignment context retained");
            var removed=CreateVoucher(4);var fourth=Repo().GetDriverTrip(4,2);
            assignments.RemoveAssignment(new AssignmentMutationRequest{TripId=4,TripRowVersion=fourth.TripRowVersion,AssignmentRowVersion=fourth.AssignmentRowVersion,Reason="Synthetic removal"},1,Utc);
            Check(Repo().GetVoucher(removed.Id,1,false).Status==FuelVoucherStatus.Cancelled && Repo().GetDriverTrip(4,2)==null,"Real removal cancels Voucher and closes assignment");
            // Existing Passenger cancellation refund remains atomic with Fuel cancellation.
            var cancelled=CreateVoucher(5);var fifth=Repo().GetDriverTrip(5,2);
            Execute(@"INSERT dbo.PassengerProfiles(UserAccountId,FirstName,LastName,CreatedUtc,UpdatedUtc) VALUES(4,N'Synthetic',N'Passenger',SYSUTCDATETIME(),SYSUTCDATETIME());
INSERT dbo.PassengerWallets(PassengerProfileId,CurrentBalance,CreatedUtc,UpdatedUtc) VALUES(1,0,SYSUTCDATETIME(),SYSUTCDATETIME());
INSERT dbo.Tickets(TicketCode,PassengerProfileId,TripId,FareAmount,TicketStatus,PurchasedUtc,UpdatedUtc) VALUES(N'TKT-SYNTHETIC',1,5,10,N'Purchased',SYSUTCDATETIME(),SYSUTCDATETIME());");
            Operations().CancelTrip(new CancelTripRequest{TripId=5,TripRowVersion=fifth.TripRowVersion,AssignmentRowVersion=fifth.AssignmentRowVersion,Reason="Synthetic cancellation"},1,Utc);
            Check(Repo().GetVoucher(cancelled.Id,1,false).Status==FuelVoucherStatus.Cancelled,"Cancellation invalidates Fuel Voucher");
            Check(Convert.ToString(Scalar("SELECT TicketStatus FROM dbo.Tickets;"))=="Refunded" && Convert.ToDecimal(Scalar("SELECT CurrentBalance FROM dbo.PassengerWallets;"))==10,"Passenger refund preserved in cancellation");
            Check(Number("SELECT COUNT(*) FROM dbo.WalletTransactions WHERE TransactionType=N'TripCancellationRefund';")==1,"Exactly one refund ledger row");
            Check(Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE TripId=5;")==0,"Cancelled Voucher creates no Fuel transaction");
            // Completion uses the real operational repository; no automatic Fuel transaction.
            var completed=CreateVoucher(6);var sixth=Repo().GetDriverTrip(6,2);
            Execute(@"INSERT dbo.PreTripInspections(TripId,TripAssignmentId,DriverProfileId,BusId,ExteriorConditionChecked,TyresSafeChecked,LightsIndicatorsChecked,NoCriticalDashboardWarningsChecked,DoorsOperationalChecked,EmergencyEquipmentPresentChecked,NoBlockingNewDefectChecked,StartOdometerKilometres,ConfirmedUtc)
SELECT TripId,TripAssignmentId,DriverProfileId,BusId,1,1,1,1,1,1,1,100,DATEADD(minute,-5,SYSUTCDATETIME()) FROM dbo.TripAssignments WHERE TripId=6 AND IsCurrent=1;
INSERT dbo.TripExecutions(TripId,PreTripInspectionId,TripAssignmentId,DriverProfileId,BusId,ActualStartUtc)
SELECT TripId,(SELECT MAX(PreTripInspectionId) FROM dbo.PreTripInspections),TripAssignmentId,DriverProfileId,BusId,DATEADD(minute,-4,SYSUTCDATETIME()) FROM dbo.TripAssignments WHERE TripId=6 AND IsCurrent=1;
UPDATE dbo.Trips SET TripStatus=N'InProgress' WHERE TripId=6;");
            sixth=Repo().GetDriverTrip(6,2);
            Operations().CompleteTrip(new CompleteTripRequest{TripId=6,TripRowVersion=sixth.TripRowVersion,AssignmentRowVersion=sixth.AssignmentRowVersion,BusRowVersion=sixth.BusRowVersion,
                RelatedRowVersion=(byte[])Scalar("SELECT RowVersion FROM dbo.TripExecutions WHERE TripId=6;"),EndOdometerKilometres=110},2,Now(6),Utc);
            Check(Repo().GetVoucher(completed.Id,1,false).Status==FuelVoucherStatus.Cancelled,"Completion cancels unused Fuel Voucher");
            Check(Number("SELECT COUNT(*) FROM dbo.Trips WHERE TripId=6 AND TripStatus=N'Completed';")==1 && Convert.ToDecimal(Scalar("SELECT OdometerKilometres FROM dbo.Buses WHERE BusId=1;"))==110,"Completion/odometer regression");
            Check(Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE TripId=6;")==0,"No invented Fuel on completion");
            // Cancellation/redemption race is serialized behind canonical locks.
            var racing=CreateVoucher(7);var seventh=Repo().GetDriverTrip(7,2);var rv=Repo().GetVoucher(racing.Id,1,false);
            var races=Race(()=>Repo().Redeem(Redemption(rv,racing.Token),FuelRedemptionToken.Hash(racing.Token),1,Now(7),Utc),
                ()=>{Operations().CancelTrip(new CancelTripRequest{TripId=7,TripRowVersion=seventh.TripRowVersion,AssignmentRowVersion=seventh.AssignmentRowVersion,Reason="Synthetic race cancellation"},1,Utc);return true;});
            Check(races[1].Error==null,"Cancellation serializes with redemption");
            var final=Repo().GetVoucher(racing.Id,1,false);
            Check(final.Status!=FuelVoucherStatus.Active,"Race leaves no invalid Active Voucher");
            Check((final.Status==FuelVoucherStatus.Redeemed&&Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE TripId=7;")==1) ||
                  (final.Status==FuelVoucherStatus.Cancelled&&Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE TripId=7;")==0),"Race consistent historical winner");
            var safety=CreateVoucher(8);var eighth=Repo().GetDriverTrip(8,2);var safetyVoucher=Repo().GetVoucher(safety.Id,1,false);
            var safetyRace=Race(()=>Repo().Redeem(Redemption(safetyVoucher,safety.Token),FuelRedemptionToken.Hash(safety.Token),1,Now(8),Utc),
                ()=>Operations().ReportDefect(new ReportDefectRequest{TripId=8,TripRowVersion=eighth.TripRowVersion,AssignmentRowVersion=eighth.AssignmentRowVersion,Category=DefectCategory.Other,Severity=DefectSeverity.Critical,Description="Synthetic race defect"},2,Now(8),Utc));
            Check(safetyRace[1].Error==null,"Critical reporting serializes behind assignment lock");
            var safetyFinal=Repo().GetVoucher(safety.Id,1,false);
            Check((safetyRace[0].Error==null && safetyFinal.Status==FuelVoucherStatus.Redeemed && Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE TripId=8;")==1) ||
                  (safetyRace[0].Error is FuelPersistenceException && safetyFinal.Status==FuelVoucherStatus.Active && Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE TripId=8;")==0),"Critical race has consistent winner; defect-first blocks redemption");
            Check(Repo().GetDriverTrip(8,2).VehicleStatus=="Operational","Critical defect does not alter Bus status");
            Check(Number("SELECT COUNT(*) FROM dbo.AuditEntries WHERE Detail LIKE N'%FMFV1.%';")==0,"No redemption secrets audited");
            Console.WriteLine(checks+" disposable-database concurrency/lifecycle checks passed. Synthetic fixtures exist only in the disposable database removed by the runner.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    sealed class Outcome{public object Value;public Exception Error;}
    static Outcome[] Race(Func<object> a,Func<object> b)
    {
        using(var gate=new Barrier(2)){
            var tasks=new[]{a,b}.Select(action=>Task.Factory.StartNew(()=>{gate.SignalAndWait();try{return new Outcome{Value=action()};}catch(Exception e){return new Outcome{Error=e};}})).ToArray();
            Task.WaitAll(tasks);return tasks.Select(x=>x.Result).ToArray();
        }
    }
    sealed class Created{public long Id;public string Token;}
    static Created CreateVoucher(long trip){long id=Repo().Submit(Submit(Repo().GetDriverTrip(trip,2)),2,Now(trip),Utc).FuelRequestId;string token=FuelRedemptionToken.Create();return new Created{Id=Repo().Approve(Approval(id),1,FuelRedemptionToken.Hash(token),Encoding.UTF8.GetBytes(token),Now(trip),Utc),Token=token};}
    static DateTime Now(long trip){return Today.AddDays(trip).AddHours(9.5);}
    static SubmitFuelRequest Submit(FuelTripContext c){return new SubmitFuelRequest{TripId=c.TripId,Justification="Synthetic verification only",SubmissionToken=Guid.NewGuid(),TripRowVersion=c.TripRowVersion,AssignmentRowVersion=c.AssignmentRowVersion};}
    static ApproveFuelRequest Approval(long id){var i=Repo().GetRequest(id,1,false);var s=Stations().GetStations(1).First();return new ApproveFuelRequest{FuelRequestId=id,FuelStationId=s.FuelStationId,Quantity=1,Amount=0,ValidUntilLocal=i.Context.ExpectedFinishLocal.AddHours(2),RequestRowVersion=i.RowVersion,TripRowVersion=i.Context.TripRowVersion,AssignmentRowVersion=i.Context.AssignmentRowVersion,BusRowVersion=i.Context.BusRowVersion,StationRowVersion=s.RowVersion};}
    static RedeemFuelVoucherRequest Redemption(FuelVoucherDetails v,string token){var s=Stations().GetStation(v.FuelStationId,1);return new RedeemFuelVoucherRequest{Token=token,FuelStationId=s.FuelStationId,VoucherRowVersion=v.RowVersion,StationRowVersion=s.RowVersion,PreviewFingerprint=FuelPolicy.Fingerprint(v,s)};}
    static object Scalar(string sql){using(var c=new SqlConnection(Connection)){c.Open();using(var cmd=new SqlCommand(sql,c)){return cmd.ExecuteScalar();}}}
    static long Number(string sql){return Convert.ToInt64(Scalar(sql));}
    static void Execute(string sql){using(var c=new SqlConnection(Connection)){c.Open();using(var cmd=new SqlCommand(sql,c)){cmd.ExecuteNonQuery();}}}
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    const string Fixture=@"
INSERT dbo.UserAccounts(RoleId,Email,NormalizedEmail,PasswordAlgorithm,PasswordHash,PasswordSalt,PasswordIterations)
VALUES(1,N'admin@invalid.example',N'ADMIN@INVALID.EXAMPLE',N'PBKDF2-HMAC-SHA256',CONVERT(varbinary(32),REPLICATE(CHAR(0),32)),CONVERT(varbinary(32),REPLICATE(CHAR(1),32)),600000),
(2,N'driver@invalid.example',N'DRIVER@INVALID.EXAMPLE',N'PBKDF2-HMAC-SHA256',CONVERT(varbinary(32),REPLICATE(CHAR(0),32)),CONVERT(varbinary(32),REPLICATE(CHAR(1),32)),600000),
(2,N'other@invalid.example',N'OTHER@INVALID.EXAMPLE',N'PBKDF2-HMAC-SHA256',CONVERT(varbinary(32),REPLICATE(CHAR(0),32)),CONVERT(varbinary(32),REPLICATE(CHAR(1),32)),600000),
(3,N'passenger@invalid.example',N'PASSENGER@INVALID.EXAMPLE',N'PBKDF2-HMAC-SHA256',CONVERT(varbinary(32),REPLICATE(CHAR(0),32)),CONVERT(varbinary(32),REPLICATE(CHAR(1),32)),600000);
INSERT dbo.StaffProfiles(UserAccountId,EmployeeNumber,FirstName,LastName) VALUES(1,N'ADM-TEST',N'Synthetic',N'Admin'),(2,N'DRV-001',N'Synthetic',N'Driver'),(3,N'DRV-002',N'Synthetic',N'Other');
INSERT dbo.DriverProfiles(StaffProfileId,DateOfBirth,AvailabilityStatus,LicenceNumber,LicenceCode,LicenceExpiryDate,PrdpNumber,PrdpCategory,PrdpExpiryDate,CreatedByUserAccountId,UpdatedByUserAccountId)
VALUES(2,'19800101',N'Available',N'TEST-A',N'EC','20990101',N'TEST-PA',N'P','20990101',1,1),(3,'19800101',N'Available',N'TEST-B',N'EC','20990101',N'TEST-PB',N'P','20990101',1,1);
INSERT dbo.Buses(BusCategoryId,PropulsionTypeId,FleetNumber,RegistrationNumber,Vin,Make,Model,ManufactureYear,PassengerCapacity,FuelTankCapacityLitres,OdometerKilometres,LicenceExpiryDate,RoadworthyExpiryDate,InsuranceExpiryDate,BaseOperationalState,GrossVehicleMassKg,CreatedByUserAccountId,UpdatedByUserAccountId)
VALUES(1,2,N'FM-TEST',N'REG-TEST',N'VIN-TEST',N'Synthetic',N'Test',2020,20,100,100,'20990101','20990101','20990101',N'Operational',8000,1,1);
INSERT dbo.Routes(RouteCode,RouteName,EstimatedDistanceKm,EstimatedDurationMinutes,DefaultFare,CreatedByUserAccountId,UpdatedByUserAccountId) VALUES(N'FM-R01',N'Synthetic verification',10,60,10,1,1);
INSERT dbo.RouteSchedules(ScheduleCode,RouteId,CreatedByUserAccountId,UpdatedByUserAccountId) VALUES(N'FM-S01',1,1,1);
INSERT dbo.RouteScheduleVersions(RouteScheduleId,RouteId,VersionNumber,EffectiveStartDate,EffectiveEndDate,ExpectedCapacity,CreatedByUserAccountId,UpdatedByUserAccountId) VALUES(1,1,1,'20200101','20990101',20,1,1);
DECLARE @i int=1,@today date=CONVERT(date,DATEADD(hour,2,SYSUTCDATETIME()));
WHILE @i<=8 BEGIN
INSERT dbo.Trips(TripCode,RouteScheduleVersionId,RouteId,ServiceDate,ScheduledDepartureTime,ExpectedFinishLocal,EstimatedDurationMinutesSnapshot,TripStatus,CreatedByUserAccountId,UpdatedByUserAccountId)
VALUES(N'TR-00000'+CONVERT(nvarchar(10),@i),1,1,DATEADD(day,@i,@today),'09:00',DATEADD(hour,10,CONVERT(datetime2(0),DATEADD(day,@i,@today))),60,N'Scheduled',1,1);
INSERT dbo.TripAssignments(TripId,DriverProfileId,BusId,IsCurrent,DecisionType,AssignedByUserAccountId,AssignedUtc,UpdatedUtc)
VALUES(@i,1,1,1,N'RecommendationAccepted',1,SYSUTCDATETIME(),SYSUTCDATETIME());
SET @i+=1;END;";
}
