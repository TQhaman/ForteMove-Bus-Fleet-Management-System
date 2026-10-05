using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Fuel;
using ForteMove.Data.Repositories;
using ForteMove.Models.Fuel;
using ForteMove.Models.Scheduling;

internal static class Slice8RollbackVerification
{
    const string Connection=@"Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True;Application Name=ForteMove.Slice8Rollback";
    static SqlConnection C;static SqlTransaction Tx;static long Trip,User,Other,Actor,StationId;
    static int checks;static DateTime Now,Utc;
    public static int Main()
    {
        try
        {
            using(C=new SqlConnection(Connection))
            {
                C.Open();
                long before=Number("SELECT (SELECT COUNT(*) FROM dbo.FuelStations)+(SELECT COUNT(*) FROM dbo.FuelRequests)+(SELECT COUNT(*) FROM dbo.FuelVouchers)+(SELECT COUNT(*) FROM dbo.FuelTransactions);");
                using(Tx=C.BeginTransaction(IsolationLevel.Serializable))
                {
                    try{Run();}finally{Tx.Rollback();}
                }
                Tx=null;
                Check(Number("SELECT (SELECT COUNT(*) FROM dbo.FuelStations)+(SELECT COUNT(*) FROM dbo.FuelRequests)+(SELECT COUNT(*) FROM dbo.FuelVouchers)+(SELECT COUNT(*) FROM dbo.FuelTransactions);")==before,"All Fuel fixture rows rolled back");
            }
            Console.WriteLine(checks+" Slice 8 SQL rollback checks passed. No rows remain; identity/rowversion counters can advance.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Run()
    {
        Call("ForteMove.Data.Internal.SqlFuelLifecycle","AcquireAssignmentLock",C,Tx);
        Call("ForteMove.Data.Internal.SqlFuelLifecycle","AcquireLock",C,Tx);
        using(var cmd=Command(@"SELECT TOP(1) t.TripId,sp.UserAccountId,
(SELECT MIN(s2.UserAccountId) FROM dbo.StaffProfiles s2 JOIN dbo.DriverProfiles d2 ON d2.StaffProfileId=s2.StaffProfileId WHERE s2.UserAccountId<>sp.UserAccountId),
(SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE r.RoleCode=N'TransportAdministrator' AND u.IsActive=1 AND u.MustChangePassword=0),t.ExpectedFinishLocal
FROM dbo.Trips t JOIN dbo.TripAssignments a ON a.TripId=t.TripId AND a.IsCurrent=1 JOIN dbo.DriverProfiles d ON d.DriverProfileId=a.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=d.StaffProfileId
WHERE t.TripStatus=N'Scheduled' AND NOT EXISTS(SELECT 1 FROM dbo.FuelRequests f WHERE f.TripId=t.TripId) ORDER BY t.TripId;"))
        using(var r=cmd.ExecuteReader())
        {
            if(!r.Read() || Enumerable.Range(0,5).Any(r.IsDBNull))throw new Exception("Requires assigned unstarted Trip, two Drivers, active Administrator.");
            Trip=r.GetInt64(0);User=r.GetInt64(1);Other=r.GetInt64(2);Actor=r.GetInt64(3);Now=r.GetDateTime(4).AddHours(-1);Utc=Now.AddHours(-2);
        }
        Execute("UPDATE dbo.UserAccounts SET MustChangePassword=0 WHERE UserAccountId=@Id OR UserAccountId=@Other;",User,Other);
        FuelTripContext context=Context();
        Check(context!=null && context.TripId==Trip,"Assigned Driver context mapping");
        Check(Call("LoadDriverTrip",C,Tx,Trip,Other)==null,"Cross-Driver Trip denied");
        // Safety data used for redemption only is temporary and rolled back.
        Execute(@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Resolved',ResolvedByUserAccountId=@Actor,ResolvedUtc=SYSUTCDATETIME(),ResolutionNote=N'Slice8 rollback fixture only'
WHERE BusId=@Id AND Severity=N'Critical' AND DefectStatus<>N'Resolved';",context.BusId);
        StationId=(long)Call(typeof(SqlFuelStationRepository).FullName,"SaveInTransaction",C,Tx,new SaveFuelStationRequest{StationName="ROLLBACK ONLY",AreaDescription="Synthetic verification",IsActive=true,SupportsDiesel=true,SupportsElectricCharging=true},Actor,Utc);
        var station=Station();
        Check(station.SupportsDiesel&&station.SupportsElectricCharging,"Station supports both supply types");
        Check(station.StationCode.StartsWith("FS-"),"Generated Station code");
        var submit=SubmitRequest();
        Expect(()=>Call("SubmitInTransaction",C,Tx,submit,Other,Now,Utc),"Wrong Driver submission");
        Expect(()=>Call("SubmitInTransaction",C,Tx,submit,Actor,Now,Utc),"Admin not Driver");
        Expect(()=>Call("SubmitInTransaction",C,Tx,submit,0L,Now,Utc),"Anonymous submission");
        var result=(FuelSubmissionResult)Call("SubmitInTransaction",C,Tx,submit,User,Now,Utc);
        Check(result.RequestCode.StartsWith("FR-"),"Generated request code");
        Check(((FuelSubmissionResult)Call("SubmitInTransaction",C,Tx,submit,User,Now,Utc)).AlreadyProcessed,"Request replay idempotent");
        Check(Number("SELECT COUNT(*) FROM dbo.AuditEntries WHERE EventType=N'FuelRequestSubmitted' AND EntityId=CONVERT(nvarchar(100),@Id);",result.FuelRequestId)==1,"No replay audit flood");
        Check(Number("SELECT COUNT(*) FROM dbo.Trips WHERE TripId=@Id AND OperationallyTouchedUtc IS NOT NULL;",Trip)==1,"Request protects Trip through canonical touch");
        Check(Call("LoadRequest",C,Tx,result.FuelRequestId,Other,true)==null,"Cross-Driver request denied");
        Expect(()=>Call("SubmitInTransaction",C,Tx,SubmitRequest(),User,Now,Utc),"Duplicate Pending assignment blocked");
        var item=Request(result.FuelRequestId);
        var approve=Approval(item);
        approve.TripRowVersion=new byte[8];
        Expect(()=>Approve(approve),"Stale approval rejected");
        approve=Approval(item);approve.Quantity=FuelPolicy.MaximumQuantity;
        Expect(()=>Approve(approve),"Above tank capacity rejected");
        approve=Approval(item);station.IsActive=false;
        Execute("UPDATE dbo.FuelStations SET IsActive=0 WHERE FuelStationId=@Id;",StationId);
        Expect(()=>Approve(approve),"Inactive Station approval rejected");
        Execute("UPDATE dbo.FuelStations SET IsActive=1 WHERE FuelStationId=@Id;",StationId);
        approve=Approval(item);string token=FuelRedemptionToken.Create();byte[] hash=FuelRedemptionToken.Hash(token);
        long voucherId=(long)Call("ApproveInTransaction",C,Tx,approve,Actor,hash,new byte[32],Now,Utc);
        Check(Request(item.FuelRequestId).Status==FuelRequestStatus.Approved,"Approval and request state atomic");
        var voucher=Voucher(voucherId);
        Check(voucher.ApprovedAmount==0&&voucher.ApprovedQuantity==approve.Quantity,"Zero Rand authorization snapshots");
        Check(voucher.ProtectedToken==null,"Admin projection excludes protected redemption token");
        Check(((FuelVoucherDetails)Call("LoadVoucher",C,Tx,voucherId,null,User,true,true)).ProtectedToken.Length==32,"Owner Driver projection includes protected token");
        Check(Call("LoadVoucher",C,Tx,voucherId,null,Other,true,true)==null,"Cross-Driver Voucher denied");
        Expect(()=>Approve(Approval(item)),"Approval cannot create second Voucher");
        Expect(()=>Call("SubmitInTransaction",C,Tx,SubmitRequest(),User,Now,Utc),"Unexpired Active Voucher prevents extra request");
        var redeem=Redemption(voucher,token);
        redeem.FuelStationId=0;Expect(()=>Redeem(redeem,hash,Now),"Wrong Station redemption");
        redeem=Redemption(voucher,token);
        Expect(()=>Redeem(redeem,FuelRedemptionToken.Hash(FuelRedemptionToken.Create()),Now),"Forged token");
        Expect(()=>Redeem(redeem,hash,voucher.ValidUntilLocal.AddSeconds(1)),"Expired redemption");
        redeem.VoucherRowVersion=new byte[8];Expect(()=>Redeem(redeem,hash,Now),"Stale Voucher review");
        // Add a valid open Cannot Proceed using exact assignment context.
        Execute(@"INSERT dbo.TripCannotProceedReports(TripId,TripAssignmentId,DriverProfileId,BusId,OccurrencePhase,Reason,ReportedUtc)
SELECT TripId,TripAssignmentId,DriverProfileId,BusId,N'PreStart',N'Rollback fuel shortage',SYSUTCDATETIME() FROM dbo.TripAssignments WHERE TripId=@Id AND IsCurrent=1;",Trip);
        Execute(@"INSERT dbo.BusDefectReports(DefectCode,BusId,ReportedByDriverProfileId,Category,Severity,Description,DefectStatus,ReportedUtc,UpdatedUtc)
VALUES(N'DF-S8-ROLLBACK',@Id,@Other,N'Other',N'Critical',N'Rollback safety fixture',N'Open',SYSUTCDATETIME(),SYSUTCDATETIME());",context.BusId,context.DriverProfileId);
        voucher=Voucher(voucherId);redeem=Redemption(voucher,token);
        Expect(()=>Redeem(redeem,hash,Now),"Critical Open defect blocks atomic redemption");
        Execute(@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Reviewed',ReviewedByUserAccountId=@Actor,ReviewedUtc=SYSUTCDATETIME(),ReviewNote=N'Rollback review' WHERE DefectCode=N'DF-S8-ROLLBACK';",0);
        Expect(()=>Redeem(redeem,hash,Now),"Critical Reviewed defect still blocks");
        Execute(@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Resolved',ResolvedByUserAccountId=@Actor,ResolvedUtc=SYSUTCDATETIME(),ResolutionNote=N'Rollback resolution' WHERE DefectCode=N'DF-S8-ROLLBACK';",0);
        // Reference edits must not rewrite Voucher snapshots.
        string snapshot=Voucher(voucherId).StationName;
        Execute("UPDATE dbo.FuelStations SET StationName=N'Changed reference' WHERE FuelStationId=@Id;",StationId);
        Expect(()=>Redeem(redeem,hash,Now),"Station change makes review stale");
        voucher=Voucher(voucherId);redeem=Redemption(voucher,token);
        decimal odo=Context().OdometerKilometres;
        Execute("UPDATE dbo.Buses SET BaseOperationalState=N'OutOfService',LicenceExpiryDate=CONVERT(date,'20200101') WHERE BusId=@Id;",context.BusId);
        var transaction=(FuelRedemptionResult)Redeem(redeem,hash,voucher.ValidUntilLocal);
        Check(transaction.TransactionCode.StartsWith("FTX-"),"Transaction code");
        Check(Voucher(voucherId).Status==FuelVoucherStatus.Redeemed,"Voucher redeemed");
        Check(Number("SELECT COUNT(*) FROM dbo.TripCannotProceedReports WHERE TripId=@Id AND ResolvedUtc IS NULL;",Trip)==1,"Fuel never auto-resolves Cannot Proceed");
        Check(Context().OdometerKilometres==odo&&Context().VehicleStatus=="OutOfService","No odometer or status side effect");
        Check(Convert.ToString(Scalar("SELECT StationNameSnapshot FROM dbo.FuelTransactions WHERE FuelVoucherId=@Id;",voucherId))==snapshot,"Immutable reference snapshot copied");
        Check(Convert.ToDecimal(Scalar("SELECT OdometerAtRedemption FROM dbo.FuelTransactions WHERE FuelVoucherId=@Id;",voucherId))==odo,"Fresh Bus odometer snapshot");
        Check(((FuelRedemptionResult)Redeem(redeem,hash,Now)).AlreadyRedeemed,"Double redemption returns existing transaction");
        Check(Number("SELECT COUNT(*) FROM dbo.FuelTransactions WHERE FuelVoucherId=@Id;",voucherId)==1,"One Transaction per Voucher");
        Check(Number("SELECT COUNT(*) FROM dbo.AuditEntries WHERE EventType=N'FuelVoucherRedeemed' AND EntityId=CONVERT(nvarchar(100),@Id);",voucherId)==1,"Exactly one redemption audit");
        Check(Number("SELECT COUNT(*) FROM dbo.AuditEntries WHERE Detail LIKE N'%FMFV1.%' OR Detail LIKE N'%ROLLBACK-TOKEN%';")==0,"No raw redemption secret in audits");
        var second=(FuelSubmissionResult)Call("SubmitInTransaction",C,Tx,SubmitRequest(),User,Now,Utc);
        Check(second.FuelRequestId!=result.FuelRequestId,"New request after redeemed Voucher permitted");
        var secondItem=Request(second.FuelRequestId);
        string token2=FuelRedemptionToken.Create();
        long voucher2=(long)Call("ApproveInTransaction",C,Tx,Approval(secondItem),Actor,FuelRedemptionToken.Hash(token2),new byte[32],Now,Utc);
        Invalidate("Assignment changed");
        Check(Voucher(voucher2).Status==FuelVoucherStatus.Cancelled&&Voucher(voucherId).Status==FuelVoucherStatus.Redeemed,"Invalidate active only, redeemed history retained");
        Check(Request(second.FuelRequestId).Status==FuelRequestStatus.Approved,"Approved request remains Approved historically");
        Expect(()=>Redeem(Redemption(Voucher(voucher2),token2),FuelRedemptionToken.Hash(token2),Now),"Cancelled Voucher denied");
        var third=(FuelSubmissionResult)Call("SubmitInTransaction",C,Tx,SubmitRequest(),User,Now,Utc);
        Invalidate("Trip completed");
        Check(Request(third.FuelRequestId).Status==FuelRequestStatus.Cancelled,"Pending invalidated on terminal lifecycle");
        // Convert only the fixture's Bus to Electric inside rollback, never invent accepted fleet data.
        Execute("UPDATE dbo.Buses SET PropulsionTypeId=4,BatteryCapacityKwh=80,FuelTankCapacityLitres=NULL WHERE BusId=@Id;",context.BusId);
        var electric=(FuelSubmissionResult)Call("SubmitInTransaction",C,Tx,SubmitRequest(),User,Now,Utc);
        Check(Request(electric.FuelRequestId).SupplyType==FuelSupplyType.ElectricCharging,"Electric derived server-side");
        long ev=(long)Call("ApproveInTransaction",C,Tx,Approval(Request(electric.FuelRequestId)),Actor,FuelRedemptionToken.Hash(FuelRedemptionToken.Create()),new byte[32],Now,Utc);
        Check(Voucher(ev).Unit==FuelQuantityUnit.KilowattHours,"Electric persisted unit kWh");
        Invalidate("Assignment removed");
        Execute("UPDATE dbo.Buses SET PropulsionTypeId=1 WHERE BusId=@Id;",context.BusId);
        Expect(()=>Call("SubmitInTransaction",C,Tx,SubmitRequest(),User,Now,Utc),"Unsupported Petrol explicitly rejected");
        var existing=(System.Collections.Generic.IList<ExistingScheduleTrip>)Call(typeof(SqlSchedulingRepository).FullName,"ReadExistingTrips",C,Tx,Number("SELECT rsv.RouteScheduleId FROM dbo.Trips t JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId WHERE TripId=@Id;",Trip),Context().ServiceDate,false);
        Check(existing.Any(x=>x.TripId==Trip&&x.HasFuelHistory&&!x.IsUntouched),"Scheduling classification retains Fuel history");
        Check(Number("SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID(N'dbo.FuelRequests'),OBJECT_ID(N'dbo.FuelVouchers'),OBJECT_ID(N'dbo.FuelTransactions'),OBJECT_ID(N'dbo.FuelStations'),OBJECT_ID(N'dbo.FuelStationCapabilities')) AND (is_disabled=1 OR is_not_trusted=1);")==0,"New checks enabled and trusted");
    }
    static void Invalidate(string reason){Call("ForteMove.Data.Internal.SqlFuelLifecycle","Invalidate",C,Tx,Trip,null,Actor,reason,Utc);}
    static object Redeem(RedeemFuelVoucherRequest r,byte[] h,DateTime n){return Call("RedeemInTransaction",C,Tx,r,h,Actor,n,Utc);}
    static object Approve(ApproveFuelRequest r){return Call("ApproveInTransaction",C,Tx,r,Actor,FuelRedemptionToken.Hash(FuelRedemptionToken.Create()),new byte[32],Now,Utc);}
    static FuelTripContext Context(){return (FuelTripContext)Call("LoadDriverTrip",C,Tx,Trip,User);}
    static FuelStation Station(){return (FuelStation)Call(typeof(SqlFuelStationRepository).FullName,"ReadStation",C,Tx,StationId);}
    static FuelRequestDetails Request(long id){return (FuelRequestDetails)Call("LoadRequest",C,Tx,id,Actor,false);}
    static FuelVoucherDetails Voucher(long id){return (FuelVoucherDetails)Call("LoadVoucher",C,Tx,id,null,Actor,false,false);}
    static SubmitFuelRequest SubmitRequest(){var c=Context();return new SubmitFuelRequest{TripId=Trip,Justification="Rollback fixture only",SubmissionToken=Guid.NewGuid(),TripRowVersion=c.TripRowVersion,AssignmentRowVersion=c.AssignmentRowVersion};}
    static ApproveFuelRequest Approval(FuelRequestDetails item){return new ApproveFuelRequest{FuelRequestId=item.FuelRequestId,Quantity=1,Amount=0,FuelStationId=StationId,ValidUntilLocal=Now.AddHours(2),RequestRowVersion=item.RowVersion,TripRowVersion=item.Context.TripRowVersion,AssignmentRowVersion=item.Context.AssignmentRowVersion,BusRowVersion=item.Context.BusRowVersion,StationRowVersion=Station().RowVersion};}
    static RedeemFuelVoucherRequest Redemption(FuelVoucherDetails v,string t){var s=Station();return new RedeemFuelVoucherRequest{Token=t,FuelStationId=StationId,VoucherRowVersion=v.RowVersion,StationRowVersion=s.RowVersion,PreviewFingerprint=FuelPolicy.Fingerprint(v,s)};}
    static object Call(string name,params object[] args){return Call(typeof(SqlFuelRepository).FullName,name,args);}
    static object Call(string type,string name,params object[] args){try{return typeof(SqlFuelRepository).Assembly.GetType(type,true).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Expect(Action action,string message){bool denied=false;try{action();}catch(FuelPersistenceException){denied=true;}Check(denied,message);}
    static SqlCommand Command(string sql){return new SqlCommand(sql,C,Tx);}
    static object Scalar(string sql,long id=0){using(var cmd=Command(sql)){cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=id;return cmd.ExecuteScalar();}}
    static long Number(string sql,long id=0){return Convert.ToInt64(Scalar(sql,id));}
    static void Execute(string sql,long id,long other=0){using(var cmd=Command(sql)){cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=id;cmd.Parameters.Add("@Other",SqlDbType.BigInt).Value=other;cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=Actor;cmd.ExecuteNonQuery();}}
    static void Check(bool result,string message){if(!result)throw new Exception(message);checks++;}
}
