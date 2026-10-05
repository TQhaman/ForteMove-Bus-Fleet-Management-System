using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Fuel;
using ForteMove.Business.Identifiers;
using ForteMove.Data.Internal;
using ForteMove.Models.Fuel;
using ForteMove.Models.Scheduling;
namespace ForteMove.Data.Repositories
{
    public sealed class SqlFuelRepository : IFuelRepository
    {
        private readonly string connectionString;
        public SqlFuelRepository(string connectionString) { this.connectionString=connectionString; }
        private const string ContextColumns=@"t.TripId,ta.TripAssignmentId,dp.DriverProfileId,b.BusId,ua.UserAccountId,t.TripCode,r.RouteName,
sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,b.FleetNumber,p.PropulsionCode,t.TripStatus,ta.IsCurrent,
CAST(CASE WHEN ua.IsActive=1 AND ua.MustChangePassword=0 AND role.IsActive=1 AND role.RoleCode=N'Driver' THEN 1 ELSE 0 END AS bit),
t.RequiresReview,t.ExpectedFinishLocal,t.ServiceDate,b.OdometerKilometres,b.FuelTankCapacityLitres,b.BatteryCapacityKwh,b.BaseOperationalState,
b.LicenceExpiryDate,b.RoadworthyExpiryDate,b.InsuranceExpiryDate,t.RowVersion,ta.RowVersion,b.RowVersion,
CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL) THEN 1 ELSE 0 END AS bit),
CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.BusDefectReports x WHERE x.BusId=b.BusId AND x.Severity=N'Critical' AND x.DefectStatus<>N'Resolved') THEN 1 ELSE 0 END AS bit),
CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.TripExecutions x WHERE x.TripId=t.TripId) THEN 1 ELSE 0 END AS bit)";
        private const string ContextFrom=@" FROM dbo.Trips t
JOIN dbo.TripAssignments ta ON ta.TripId=t.TripId
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId
JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
JOIN dbo.Roles role ON role.RoleId=ua.RoleId
JOIN dbo.Buses b ON b.BusId=ta.BusId JOIN dbo.PropulsionTypes p ON p.PropulsionTypeId=b.PropulsionTypeId
JOIN dbo.Routes r ON r.RouteId=t.RouteId ";
        private static string From(bool locked)
        {
            if(!locked) return ContextFrom;
            return ContextFrom.Replace("dbo.Trips t","dbo.Trips t WITH(UPDLOCK,HOLDLOCK)")
                .Replace("dbo.TripAssignments ta","dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK)")
                .Replace("dbo.DriverProfiles dp","dbo.DriverProfiles dp WITH(UPDLOCK,HOLDLOCK)")
                .Replace("dbo.StaffProfiles sp","dbo.StaffProfiles sp WITH(UPDLOCK,HOLDLOCK)")
                .Replace("dbo.UserAccounts ua","dbo.UserAccounts ua WITH(UPDLOCK,HOLDLOCK)")
                .Replace("dbo.Buses b","dbo.Buses b WITH(UPDLOCK,HOLDLOCK)");
        }
        private const string RequestColumns=@",f.FuelRequestId,f.RequestCode,f.FuelSupplyType,f.Justification,f.RequestStatus,f.OdometerAtRequest,
f.SubmittedUtc,f.DecisionNote,f.CancellationReason,f.RowVersion,(SELECT v.FuelVoucherId FROM dbo.FuelVouchers v WHERE v.FuelRequestId=f.FuelRequestId)";
        public IList<FuelTripContext> GetEligibleTrips(long user)
        {
            return Read(user,true,(c)=> {
                var list=new List<FuelTripContext>();
                using(var cmd=SqlFuelLifecycle.Command(c,null,"SELECT "+ContextColumns+From(false)+@"WHERE ua.UserAccountId=@User AND ta.IsCurrent=1
AND t.TripStatus IN(N'Scheduled',N'Ready',N'InProgress',N'Delayed') AND p.PropulsionCode IN(N'Diesel',N'Electric') ORDER BY t.ServiceDate,t.ScheduledDepartureTime;"))
                { SqlFuelLifecycle.Id(cmd,"@User",user);using(var r=cmd.ExecuteReader()) while(r.Read()) list.Add(MapContext(r)); }
                return (IList<FuelTripContext>)list;
            });
        }
        public FuelTripContext GetDriverTrip(long trip,long user)
        { return Read(user,true,c=>LoadDriverTrip(c,null,trip,user)); }
        private static FuelTripContext LoadDriverTrip(SqlConnection c,SqlTransaction tx,long trip,long user)
        {
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"SELECT "+ContextColumns+From(tx!=null)+"WHERE t.TripId=@Trip AND ua.UserAccountId=@User AND ta.IsCurrent=1;"))
            { SqlFuelLifecycle.Id(cmd,"@Trip",trip);SqlFuelLifecycle.Id(cmd,"@User",user);using(var r=cmd.ExecuteReader()) return r.Read()?MapContext(r):null; }
        }
        public IList<FuelRequestDetails> GetRequests(FuelQuery query,long user,bool driver)
        {
            return Read(user,driver,c=>{
                var list=new List<FuelRequestDetails>();
                using(var cmd=SqlFuelLifecycle.Command(c,null,"SELECT TOP(200) "+ContextColumns+RequestColumns+From(false)+@"JOIN dbo.FuelRequests f ON f.TripAssignmentId=ta.TripAssignmentId
WHERE (@Driver=0 OR ua.UserAccountId=@User) AND (@Status IS NULL OR f.RequestStatus=@Status)
AND (@Search IS NULL OR f.RequestCode LIKE @Search OR t.TripCode LIKE @Search OR b.FleetNumber LIKE @Search OR sp.EmployeeNumber LIKE @Search)
ORDER BY f.SubmittedUtc DESC,f.FuelRequestId DESC;"))
                { QueryParameters(cmd,query,user,driver);using(var r=cmd.ExecuteReader()) while(r.Read()) list.Add(MapRequest(r)); }
                return (IList<FuelRequestDetails>)list;
            });
        }
        public FuelRequestDetails GetRequest(long id,long user,bool driver)
        { return Read(user,driver,c=>LoadRequest(c,null,id,user,driver)); }
        private static FuelRequestDetails LoadRequest(SqlConnection c,SqlTransaction tx,long id,long user,bool driver)
        {
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"SELECT "+ContextColumns+RequestColumns+From(tx!=null)+
                (tx==null?"JOIN dbo.FuelRequests f ":"JOIN dbo.FuelRequests f WITH(UPDLOCK,HOLDLOCK) ")+@"ON f.TripAssignmentId=ta.TripAssignmentId
WHERE f.FuelRequestId=@Id AND (@Driver=0 OR ua.UserAccountId=@User);"))
            { SqlFuelLifecycle.Id(cmd,"@Id",id);SqlFuelLifecycle.Id(cmd,"@User",user);cmd.Parameters.Add("@Driver",SqlDbType.Bit).Value=driver;
                using(var r=cmd.ExecuteReader()) return r.Read()?MapRequest(r):null; }
        }
        public FuelSubmissionResult Submit(SubmitFuelRequest request,long user,DateTime now,DateTime utc)
        { return Write(user,true,(c,tx)=>SubmitInTransaction(c,tx,request,user,now,utc)); }
        internal static FuelSubmissionResult SubmitInTransaction(SqlConnection c,SqlTransaction tx,SubmitFuelRequest request,long user,DateTime now,DateTime utc)
        {
            Locks(c,tx,user,true);
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"SELECT FuelRequestId,RequestCode,TripId,SubmittedByUserAccountId FROM dbo.FuelRequests WITH(UPDLOCK,HOLDLOCK) WHERE SubmissionToken=@Token;"))
            {
                cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=request.SubmissionToken;
                using(var r=cmd.ExecuteReader()) if(r.Read())
                {
                    if(r.GetInt64(2)!=request.TripId || r.GetInt64(3)!=user) throw new FuelPersistenceException("This submission has already been used.");
                    return new FuelSubmissionResult { FuelRequestId=r.GetInt64(0),RequestCode=r.GetString(1),AlreadyProcessed=true };
                }
            }
            var context=LoadDriverTrip(c,tx,request.TripId,user);
            if(context!=null && !FuelPolicy.Supply(context.PropulsionCode).HasValue) throw new FuelPersistenceException("Fuel Requests support Diesel and Electric Buses only.");
            if(!FuelPolicy.Eligible(context)) throw new FuelPersistenceException("Only your current assigned, nonterminal Trip can request fuel.");
            Versions(context.TripRowVersion,request.TripRowVersion);Versions(context.AssignmentRowVersion,request.AssignmentRowVersion);
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"SELECT COUNT(*) FROM dbo.FuelRequests WITH(UPDLOCK,HOLDLOCK) WHERE TripAssignmentId=@Assignment AND RequestStatus=N'Pending';"))
            { SqlFuelLifecycle.Id(cmd,"@Assignment",context.TripAssignmentId);if(Convert.ToInt32(cmd.ExecuteScalar())>0) throw new FuelPersistenceException("This assignment already has a Pending Fuel Request."); }
            Outstanding(c,tx,context.TripAssignmentId,now);
            string code=IdentifierCodePolicy.FormatFuelRequestCode(SqlFuelLifecycle.Next(c,tx,"FuelRequests","RequestCode",4));
            long id;
            using(var cmd=SqlFuelLifecycle.Command(c,tx,@"INSERT dbo.FuelRequests(RequestCode,TripAssignmentId,TripId,DriverProfileId,BusId,FuelSupplyType,Justification,OdometerAtRequest,SubmissionToken,RequestStatus,SubmittedByUserAccountId,SubmittedUtc,UpdatedUtc)
VALUES(@Code,@Assignment,@Trip,@Driver,@Bus,@Supply,@Reason,@Odo,@Token,N'Pending',@User,@Utc,@Utc);SELECT CONVERT(bigint,SCOPE_IDENTITY());"))
            {
                SqlFuelLifecycle.Text(cmd,"@Code",30,code);ContextParameters(cmd,context);SqlFuelLifecycle.Text(cmd,"@Supply",30,FuelPolicy.Supply(context.PropulsionCode).Value.ToString());
                SqlFuelLifecycle.Text(cmd,"@Reason",500,request.Justification);SqlFuelLifecycle.Decimal(cmd,"@Odo",12,1,context.OdometerKilometres);
                cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=request.SubmissionToken;SqlFuelLifecycle.Id(cmd,"@User",user);SqlFuelLifecycle.Utc(cmd,"@Utc",utc);
                id=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);
            }
            Touch(c,tx,context.TripId,user,utc);
            Audit(c,tx,user,"FuelRequestSubmitted","FuelRequest",id,"Code="+code+";Trip="+context.TripCode,utc);
            return new FuelSubmissionResult { FuelRequestId=id,RequestCode=code };
        }
        public long Approve(ApproveFuelRequest request,long actor,byte[] tokenHash,byte[] protectedToken,DateTime now,DateTime utc)
        { return Write(actor,false,(c,tx)=>ApproveInTransaction(c,tx,request,actor,tokenHash,protectedToken,now,utc)); }
        internal static long ApproveInTransaction(SqlConnection c,SqlTransaction tx,ApproveFuelRequest request,long actor,byte[] tokenHash,byte[] protectedToken,DateTime now,DateTime utc)
        {
            Locks(c,tx,actor,false);
            var item=LoadRequest(c,tx,request.FuelRequestId,actor,false);
            var station=SqlFuelStationRepository.ReadStation(c,tx,request.FuelStationId);
            var errors=FuelPolicy.ValidateApproval(item,station,request,now);
            if(errors.Count>0) throw new FuelPersistenceException(string.Join(" ",errors.Select(x=>x.Message)));
            Versions(item.RowVersion,request.RequestRowVersion);Versions(item.Context.TripRowVersion,request.TripRowVersion);
            Versions(item.Context.AssignmentRowVersion,request.AssignmentRowVersion);Versions(item.Context.BusRowVersion,request.BusRowVersion);
            Versions(station.RowVersion,request.StationRowVersion);Outstanding(c,tx,item.Context.TripAssignmentId,now);
            if(tokenHash==null || tokenHash.Length!=32 || protectedToken==null || protectedToken.Length==0 || protectedToken.Length>512)
                throw new FuelPersistenceException("The redemption authorization could not be protected.");
            string code=IdentifierCodePolicy.FormatFuelVoucherCode(SqlFuelLifecycle.Next(c,tx,"FuelVouchers","VoucherCode",4));
            long id;
            using(var cmd=SqlFuelLifecycle.Command(c,tx,@"INSERT dbo.FuelVouchers(VoucherCode,FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelSupplyType,QuantityUnit,ApprovedQuantity,ApprovedAmount,FuelStationId,
StationCodeSnapshot,StationNameSnapshot,StationAreaSnapshot,FleetNumberSnapshot,ApprovedByUserAccountId,ApprovedUtc,ApprovalNote,ValidUntilLocal,RedemptionTokenHash,ProtectedRedemptionToken,VoucherStatus,UpdatedUtc)
VALUES(@Code,@Request,@Assignment,@Trip,@Driver,@Bus,@Supply,@Unit,@Quantity,@Amount,@Station,@StationCode,@StationName,@StationArea,@Fleet,@Actor,@Utc,@Note,@Until,@Hash,@Protected,N'Active',@Utc);
SELECT CONVERT(bigint,SCOPE_IDENTITY());"))
            {
                SqlFuelLifecycle.Text(cmd,"@Code",30,code);SqlFuelLifecycle.Id(cmd,"@Request",item.FuelRequestId);ContextParameters(cmd,item.Context);
                SqlFuelLifecycle.Text(cmd,"@Supply",30,item.SupplyType.ToString());SqlFuelLifecycle.Text(cmd,"@Unit",20,FuelPolicy.Unit(item.SupplyType).ToString());
                SqlFuelLifecycle.Decimal(cmd,"@Quantity",10,2,request.Quantity.Value);SqlFuelLifecycle.Decimal(cmd,"@Amount",12,2,request.Amount.Value);
                SqlFuelLifecycle.Id(cmd,"@Station",station.FuelStationId);SqlFuelLifecycle.Text(cmd,"@StationCode",20,station.StationCode);
                SqlFuelLifecycle.Text(cmd,"@StationName",150,station.StationName);SqlFuelLifecycle.Text(cmd,"@StationArea",250,station.AreaDescription);
                SqlFuelLifecycle.Text(cmd,"@Fleet",30,item.Context.FleetNumber);SqlFuelLifecycle.Id(cmd,"@Actor",actor);SqlFuelLifecycle.Utc(cmd,"@Utc",utc);
                SqlFuelLifecycle.Text(cmd,"@Note",500,request.Note);SqlFuelLifecycle.Utc(cmd,"@Until",request.ValidUntilLocal.Value);
                cmd.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=tokenHash;cmd.Parameters.Add("@Protected",SqlDbType.VarBinary,512).Value=protectedToken;
                id=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);
            }
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"UPDATE dbo.FuelRequests SET RequestStatus=N'Approved',DecisionByUserAccountId=@Actor,DecisionUtc=@Utc,DecisionNote=@Note,UpdatedUtc=@Utc WHERE FuelRequestId=@Id;"))
            { SqlFuelLifecycle.Id(cmd,"@Actor",actor);SqlFuelLifecycle.Id(cmd,"@Id",item.FuelRequestId);SqlFuelLifecycle.Utc(cmd,"@Utc",utc);SqlFuelLifecycle.Text(cmd,"@Note",500,request.Note);cmd.ExecuteNonQuery(); }
            Audit(c,tx,actor,"FuelRequestApproved","FuelRequest",item.FuelRequestId,"Voucher="+code,utc);
            Audit(c,tx,actor,"FuelVoucherCreated","FuelVoucher",id,"Code="+code+";Station="+station.StationCode,utc);
            return id;
        }
        public void Reject(RejectFuelRequest request,long actor,DateTime utc)
        {
            Write(actor,false,(c,tx)=>{
                using(var cmd=SqlFuelLifecycle.Command(c,tx,@"UPDATE dbo.FuelRequests SET RequestStatus=N'Rejected',DecisionByUserAccountId=@Actor,DecisionUtc=@Utc,DecisionNote=@Reason,UpdatedUtc=@Utc
WHERE FuelRequestId=@Id AND RequestStatus=N'Pending' AND RowVersion=@Rv;"))
                { SqlFuelLifecycle.Id(cmd,"@Actor",actor);SqlFuelLifecycle.Id(cmd,"@Id",request.FuelRequestId);SqlFuelLifecycle.Utc(cmd,"@Utc",utc);
                    SqlFuelLifecycle.Text(cmd,"@Reason",500,request.Reason);SqlFuelLifecycle.Version(cmd,"@Rv",request.RowVersion);
                    if(cmd.ExecuteNonQuery()!=1) throw new FuelPersistenceException("This request is no longer Pending or has changed."); }
                Audit(c,tx,actor,"FuelRequestRejected","FuelRequest",request.FuelRequestId,"Reason="+request.Reason,utc);return true;
            });
        }
        private const string VoucherSelect=@"SELECT v.FuelVoucherId,v.VoucherCode,v.FuelRequestId,v.FuelStationId,v.StationCodeSnapshot,v.StationNameSnapshot,v.StationAreaSnapshot,v.FleetNumberSnapshot,
v.FuelSupplyType,v.QuantityUnit,v.ApprovedQuantity,v.ApprovedAmount,v.ApprovedUtc,v.ValidUntilLocal,v.VoucherStatus,v.RedeemedUtc,v.CancellationReason,v.RowVersion,";
        public IList<FuelVoucherDetails> GetVouchers(FuelQuery query,long user,bool driver)
        {
            return Read(user,driver,c=>{
                var ids=new List<long>();
                using(var cmd=SqlFuelLifecycle.Command(c,null,@"SELECT TOP(200) v.FuelVoucherId FROM dbo.FuelVouchers v JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=v.DriverProfileId
JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
WHERE (@Driver=0 OR sp.UserAccountId=@User) AND (@Search IS NULL OR v.VoucherCode LIKE @Search OR v.FleetNumberSnapshot LIKE @Search OR v.StationNameSnapshot LIKE @Search)
ORDER BY v.ApprovedUtc DESC,v.FuelVoucherId DESC;"))
                { QueryParameters(cmd,query,user,driver);using(var r=cmd.ExecuteReader()) while(r.Read()) ids.Add(r.GetInt64(0)); }
                return (IList<FuelVoucherDetails>)ids.Select(id=>LoadVoucher(c,null,id,null,user,driver,false)).ToList();
            });
        }
        public FuelVoucherDetails GetVoucher(long id,long user,bool driver) { return Read(user,driver,c=>LoadVoucher(c,null,id,null,user,driver,driver)); }
        public FuelVoucherDetails FindVoucher(byte[] hash,long actor) { return Read(actor,false,c=>LoadVoucher(c,null,0,hash,actor,false,false)); }
        internal static FuelVoucherDetails LoadVoucher(SqlConnection c,SqlTransaction tx,long id,byte[] hash,long user,bool driver,bool includeProtected)
        {
            FuelVoucherDetails result;
            string sql=VoucherSelect+(includeProtected?"v.ProtectedRedemptionToken":"CAST(NULL AS varbinary(512))")+" FROM dbo.FuelVouchers v "+
                (tx==null?"":"WITH(UPDLOCK,HOLDLOCK) ")+@"JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=v.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
WHERE ((@Hash IS NULL AND v.FuelVoucherId=@Id) OR v.RedemptionTokenHash=@Hash) AND (@Driver=0 OR sp.UserAccountId=@User);";
            using(var cmd=SqlFuelLifecycle.Command(c,tx,sql))
            {
                SqlFuelLifecycle.Id(cmd,"@Id",id);SqlFuelLifecycle.Id(cmd,"@User",user);cmd.Parameters.Add("@Driver",SqlDbType.Bit).Value=driver;
                cmd.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=(object)hash??DBNull.Value;
                using(var r=cmd.ExecuteReader())
                {
                    if(!r.Read()) return null;
                    result=new FuelVoucherDetails { FuelVoucherId=r.GetInt64(0),VoucherCode=r.GetString(1),Request=new FuelRequestDetails{FuelRequestId=r.GetInt64(2)},
                        FuelStationId=r.GetInt64(3),StationCode=r.GetString(4),StationName=r.GetString(5),StationArea=r.GetString(6),FleetNumber=r.GetString(7),
                        SupplyType=Parse<FuelSupplyType>(r.GetString(8)),Unit=Parse<FuelQuantityUnit>(r.GetString(9)),ApprovedQuantity=r.GetDecimal(10),ApprovedAmount=r.GetDecimal(11),
                        ApprovedUtc=r.GetDateTime(12),ValidUntilLocal=r.GetDateTime(13),Status=Parse<FuelVoucherStatus>(r.GetString(14)),
                        RedeemedUtc=r.IsDBNull(15)?(DateTime?)null:r.GetDateTime(15),CancellationReason=r.IsDBNull(16)?null:r.GetString(16),
                        RowVersion=(byte[])r.GetValue(17),ProtectedToken=r.IsDBNull(18)?null:(byte[])r.GetValue(18) };
                }
            }
            result.Request=LoadRequest(c,tx,result.Request.FuelRequestId,user,driver);
            return result;
        }
        public FuelRedemptionResult Redeem(RedeemFuelVoucherRequest request,byte[] hash,long actor,DateTime now,DateTime utc)
        { return Write(actor,false,(c,tx)=>RedeemInTransaction(c,tx,request,hash,actor,now,utc)); }
        internal static FuelRedemptionResult RedeemInTransaction(SqlConnection c,SqlTransaction tx,RedeemFuelVoucherRequest request,byte[] hash,long actor,DateTime now,DateTime utc)
        {
            Locks(c,tx,actor,false);
            var voucher=LoadVoucher(c,tx,0,hash,actor,false,false);
            if(voucher==null) throw new FuelPersistenceException("The Voucher token is invalid or unavailable.");
            if(voucher.FuelStationId!=request.FuelStationId) throw new FuelPersistenceException("Choose the approved Fuel Station.");
            if(voucher.Status==FuelVoucherStatus.Redeemed)
            {
                using(var cmd=SqlFuelLifecycle.Command(c,tx,"SELECT FuelTransactionId,TransactionCode FROM dbo.FuelTransactions WHERE FuelVoucherId=@Id;"))
                { SqlFuelLifecycle.Id(cmd,"@Id",voucher.FuelVoucherId);using(var r=cmd.ExecuteReader()) if(r.Read()) return new FuelRedemptionResult{FuelTransactionId=r.GetInt64(0),TransactionCode=r.GetString(1),AlreadyRedeemed=true}; }
                throw new FuelPersistenceException("Already redeemed.");
            }
            var station=SqlFuelStationRepository.ReadStation(c,tx,request.FuelStationId);
            string block=FuelPolicy.RedemptionBlock(voucher,station,now);
            if(block!=null) throw new FuelPersistenceException(block);
            Versions(voucher.RowVersion,request.VoucherRowVersion);Versions(station.RowVersion,request.StationRowVersion);
            if(request.PreviewFingerprint!=FuelPolicy.Fingerprint(voucher,station)) throw new FuelPersistenceException("The reviewed Voucher changed. Review it again.");
            string code=IdentifierCodePolicy.FormatFuelTransactionCode(SqlFuelLifecycle.Next(c,tx,"FuelTransactions","TransactionCode",5));
            long id;
            using(var cmd=SqlFuelLifecycle.Command(c,tx,@"INSERT dbo.FuelTransactions(TransactionCode,FuelVoucherId,FuelRequestId,TripAssignmentId,TripId,DriverProfileId,BusId,FuelStationId,FuelSupplyType,QuantityUnit,
Quantity,Amount,StationCodeSnapshot,StationNameSnapshot,StationAreaSnapshot,FleetNumberSnapshot,OdometerAtRedemption,RedeemedUtc,InitiatedByUserAccountId)
SELECT @Code,v.FuelVoucherId,v.FuelRequestId,v.TripAssignmentId,v.TripId,v.DriverProfileId,v.BusId,v.FuelStationId,v.FuelSupplyType,v.QuantityUnit,
v.ApprovedQuantity,v.ApprovedAmount,v.StationCodeSnapshot,v.StationNameSnapshot,v.StationAreaSnapshot,v.FleetNumberSnapshot,@Odo,@Utc,@Actor FROM dbo.FuelVouchers v WHERE v.FuelVoucherId=@Id;
SELECT CONVERT(bigint,SCOPE_IDENTITY());"))
            { SqlFuelLifecycle.Text(cmd,"@Code",30,code);SqlFuelLifecycle.Id(cmd,"@Id",voucher.FuelVoucherId);SqlFuelLifecycle.Decimal(cmd,"@Odo",12,1,voucher.Request.Context.OdometerKilometres);
                SqlFuelLifecycle.Utc(cmd,"@Utc",utc);SqlFuelLifecycle.Id(cmd,"@Actor",actor);id=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture); }
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"UPDATE dbo.FuelVouchers SET VoucherStatus=N'Redeemed',RedeemedUtc=@Utc,RedeemedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE FuelVoucherId=@Id AND VoucherStatus=N'Active';"))
            { SqlFuelLifecycle.Id(cmd,"@Id",voucher.FuelVoucherId);SqlFuelLifecycle.Id(cmd,"@Actor",actor);SqlFuelLifecycle.Utc(cmd,"@Utc",utc);if(cmd.ExecuteNonQuery()!=1) throw new FuelPersistenceException("The Voucher was already consumed."); }
            Audit(c,tx,actor,"FuelVoucherRedeemed","FuelVoucher",voucher.FuelVoucherId,"Transaction="+code,utc);
            Audit(c,tx,actor,"FuelTransactionRecorded","FuelTransaction",id,"Voucher="+voucher.VoucherCode+";Station="+voucher.StationCode,utc);
            return new FuelRedemptionResult { FuelTransactionId=id,TransactionCode=code };
        }
        private const string TransactionSelect=@"SELECT x.FuelTransactionId,x.TransactionCode,x.FuelVoucherId,v.VoucherCode,f.RequestCode,t.TripCode,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,
x.FleetNumberSnapshot,x.StationCodeSnapshot,x.StationNameSnapshot,x.StationAreaSnapshot,x.FuelSupplyType,x.QuantityUnit,x.Quantity,x.Amount,x.OdometerAtRedemption,x.RedeemedUtc
FROM dbo.FuelTransactions x JOIN dbo.FuelVouchers v ON v.FuelVoucherId=x.FuelVoucherId JOIN dbo.FuelRequests f ON f.FuelRequestId=x.FuelRequestId
JOIN dbo.Trips t ON t.TripId=x.TripId JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=x.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId ";
        public IList<FuelTransactionDetails> GetTransactions(FuelQuery query,long actor)
        {
            return Read(actor,false,c=>{
                var list=new List<FuelTransactionDetails>();
                using(var cmd=SqlFuelLifecycle.Command(c,null,TransactionSelect+"WHERE (@Search IS NULL OR x.TransactionCode LIKE @Search OR x.FleetNumberSnapshot LIKE @Search OR x.StationNameSnapshot LIKE @Search) ORDER BY x.RedeemedUtc DESC,x.FuelTransactionId DESC;"))
                { QueryParameters(cmd,query,actor,false);using(var r=cmd.ExecuteReader()) while(r.Read()) list.Add(MapTransaction(r)); }
                return (IList<FuelTransactionDetails>)list;
            });
        }
        public FuelTransactionDetails GetTransaction(long id,long actor)
        {
            return Read(actor,false,c=>{
                using(var cmd=SqlFuelLifecycle.Command(c,null,TransactionSelect+"WHERE x.FuelTransactionId=@Id;"))
                { SqlFuelLifecycle.Id(cmd,"@Id",id);using(var r=cmd.ExecuteReader()) return r.Read()?MapTransaction(r):null; }
            });
        }
        private T Read<T>(long user,bool driver,Func<SqlConnection,T> action)
        {
            using(var connection=new SqlConnection(connectionString)) { connection.Open();SqlFuelLifecycle.Authorise(connection,null,user,driver);return action(connection); }
        }
        private T Write<T>(long user,bool driver,Func<SqlConnection,SqlTransaction,T> action)
        {
            try { using(var connection=new SqlConnection(connectionString)) { connection.Open();using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                { Locks(connection,transaction,user,driver);var result=action(connection,transaction);transaction.Commit();return result; } } }
            catch(SqlException error)
            {
                string message=error.Number>=51100 && error.Number<=51199?error.Message:"The Fuel action could not be saved because its authorization changed. Refresh and try again.";
                throw new FuelPersistenceException(message,error);
            }
        }
        private static void Locks(SqlConnection c,SqlTransaction tx,long user,bool driver)
        { SqlFuelLifecycle.AcquireAssignmentLock(c,tx);SqlFuelLifecycle.AcquireLock(c,tx);SqlFuelLifecycle.Authorise(c,tx,user,driver); }
        private static void Outstanding(SqlConnection c,SqlTransaction tx,long assignment,DateTime now)
        {
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"SELECT COUNT(*) FROM dbo.FuelVouchers WITH(UPDLOCK,HOLDLOCK) WHERE TripAssignmentId=@Assignment AND VoucherStatus=N'Active' AND ValidUntilLocal>=@Now;"))
            { SqlFuelLifecycle.Id(cmd,"@Assignment",assignment);var current=cmd.Parameters.Add("@Now",SqlDbType.DateTime2);current.Scale=7;current.Value=now;if(Convert.ToInt32(cmd.ExecuteScalar())>0) throw new FuelPersistenceException("This assignment already has an unexpired Active Voucher."); }
        }
        private static void Versions(byte[] current,byte[] reviewed) { if(!SqlFuelLifecycle.Same(current,reviewed)) throw new FuelPersistenceException("The request, assignment, Bus or Station changed. Reload before continuing."); }
        private static void ContextParameters(SqlCommand cmd,FuelTripContext item)
        { SqlFuelLifecycle.Id(cmd,"@Trip",item.TripId);SqlFuelLifecycle.Id(cmd,"@Assignment",item.TripAssignmentId);SqlFuelLifecycle.Id(cmd,"@Driver",item.DriverProfileId);SqlFuelLifecycle.Id(cmd,"@Bus",item.BusId); }
        private static void QueryParameters(SqlCommand cmd,FuelQuery query,long user,bool driver)
        {
            SqlFuelLifecycle.Id(cmd,"@User",user);cmd.Parameters.Add("@Driver",SqlDbType.Bit).Value=driver;
            string search=query==null?null:query.Search;
            if(!string.IsNullOrWhiteSpace(search)) search="%"+search.Trim().Substring(0,Math.Min(100,search.Trim().Length)).Replace("[","[[]").Replace("%","[%]").Replace("_","[_]")+"%";else search=null;
            SqlFuelLifecycle.Text(cmd,"@Search",410,search);SqlFuelLifecycle.Text(cmd,"@Status",20,query==null || string.IsNullOrWhiteSpace(query.Status)?null:query.Status);
        }
        private static void Touch(SqlConnection c,SqlTransaction tx,long trip,long actor,DateTime utc)
        {
            using(var cmd=SqlFuelLifecycle.Command(c,tx,"UPDATE dbo.Trips SET OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@Actor),UpdatedUtc=@Utc,UpdatedByUserAccountId=@Actor WHERE TripId=@Trip;"))
            { SqlFuelLifecycle.Id(cmd,"@Trip",trip);SqlFuelLifecycle.Id(cmd,"@Actor",actor);SqlFuelLifecycle.Utc(cmd,"@Utc",utc);cmd.ExecuteNonQuery(); }
        }
        private static void Audit(SqlConnection c,SqlTransaction tx,long actor,string type,string entity,long id,string detail,DateTime utc)
        { SqlAuditWriter.Write(c,tx,actor,type,entity,id.ToString(CultureInfo.InvariantCulture),detail,null,utc); }
        private static T Parse<T>(string value) { return (T)Enum.Parse(typeof(T),value,false); }
        private static FuelTripContext MapContext(SqlDataReader r)
        {
            return new FuelTripContext { TripId=r.GetInt64(0),TripAssignmentId=r.GetInt64(1),DriverProfileId=r.GetInt64(2),BusId=r.GetInt64(3),DriverUserAccountId=r.GetInt64(4),
                TripCode=r.GetString(5),RouteName=r.GetString(6),DriverName=r.GetString(7),EmployeeNumber=r.GetString(8),FleetNumber=r.GetString(9),PropulsionCode=r.GetString(10),
                TripStatus=Parse<TripStatus>(r.GetString(11)),AssignmentIsCurrent=r.GetBoolean(12),DriverAccountActive=r.GetBoolean(13),RequiresReview=r.GetBoolean(14),
                ExpectedFinishLocal=r.GetDateTime(15),ServiceDate=r.GetDateTime(16),OdometerKilometres=r.GetDecimal(17),FuelTankCapacityLitres=r.IsDBNull(18)?(decimal?)null:r.GetDecimal(18),
                BatteryCapacityKwh=r.IsDBNull(19)?(decimal?)null:r.GetDecimal(19),VehicleStatus=r.GetString(20),LicenceExpiryDate=r.GetDateTime(21),
                RoadworthyExpiryDate=r.GetDateTime(22),InsuranceExpiryDate=r.GetDateTime(23),TripRowVersion=(byte[])r.GetValue(24),AssignmentRowVersion=(byte[])r.GetValue(25),
                BusRowVersion=(byte[])r.GetValue(26),HasOpenCannotProceed=r.GetBoolean(27),HasCriticalDefect=r.GetBoolean(28),HasExecution=r.GetBoolean(29) };
        }
        private static FuelRequestDetails MapRequest(SqlDataReader r)
        {
            return new FuelRequestDetails { Context=MapContext(r),FuelRequestId=r.GetInt64(30),RequestCode=r.GetString(31),SupplyType=Parse<FuelSupplyType>(r.GetString(32)),
                Justification=r.GetString(33),Status=Parse<FuelRequestStatus>(r.GetString(34)),OdometerAtRequest=r.GetDecimal(35),SubmittedUtc=r.GetDateTime(36),
                DecisionNote=r.IsDBNull(37)?null:r.GetString(37),CancellationReason=r.IsDBNull(38)?null:r.GetString(38),RowVersion=(byte[])r.GetValue(39),
                FuelVoucherId=r.IsDBNull(40)?(long?)null:r.GetInt64(40) };
        }
        private static FuelTransactionDetails MapTransaction(SqlDataReader r)
        {
            return new FuelTransactionDetails { FuelTransactionId=r.GetInt64(0),TransactionCode=r.GetString(1),FuelVoucherId=r.GetInt64(2),VoucherCode=r.GetString(3),RequestCode=r.GetString(4),
                TripCode=r.GetString(5),DriverName=r.GetString(6),EmployeeNumber=r.GetString(7),FleetNumber=r.GetString(8),StationCode=r.GetString(9),StationName=r.GetString(10),
                StationArea=r.GetString(11),SupplyType=Parse<FuelSupplyType>(r.GetString(12)),Unit=Parse<FuelQuantityUnit>(r.GetString(13)),Quantity=r.GetDecimal(14),
                Amount=r.GetDecimal(15),OdometerAtRedemption=r.GetDecimal(16),RedeemedUtc=r.GetDateTime(17) };
        }
    }
}
