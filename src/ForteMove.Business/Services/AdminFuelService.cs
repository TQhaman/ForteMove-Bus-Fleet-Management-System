using System;
using System.Collections.Generic;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Fuel;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fuel;
namespace ForteMove.Business.Services
{
    public sealed class AdminFuelService
    {
        private readonly IFuelRepository repository;
        private readonly IFuelStationRepository stations;
        private readonly IFuelTokenProtector protector;
        private readonly IClock clock;
        public AdminFuelService(IFuelRepository repository,IFuelStationRepository stations,IFuelTokenProtector protector,IClock clock=null)
        { this.repository=repository;this.stations=stations;this.protector=protector;this.clock=clock??new SystemClock(); }
        public IList<FuelRequestDetails> GetRequests(FuelQuery query,long actor) { return repository.GetRequests(query,actor,false); }
        public FuelRequestDetails GetRequest(long id,long actor) { return repository.GetRequest(id,actor,false); }
        public IList<FuelVoucherDetails> GetVouchers(FuelQuery query,long actor)
        { var items=repository.GetVouchers(query,actor,false);foreach(var item in items) item.DisplayState=FuelPolicy.DisplayState(item,clock.OperationalNow);return items; }
        public FuelVoucherDetails GetVoucher(long id,long actor)
        { var item=repository.GetVoucher(id,actor,false);if(item!=null) item.DisplayState=FuelPolicy.DisplayState(item,clock.OperationalNow);return item; }
        public IList<FuelTransactionDetails> GetTransactions(FuelQuery query,long actor) { return repository.GetTransactions(query,actor); }
        public FuelTransactionDetails GetTransaction(long id,long actor) { return repository.GetTransaction(id,actor); }
        public ServiceResult<long> Approve(ApproveFuelRequest request,long actor)
        {
            if(request!=null && request.FuelStationId<=0)
                return ServiceResult<long>.Failure("FuelStationId","Select an active compatible Fuel Station before approving.");
            if(request==null || !FuelPolicy.TokenVersion(request.RequestRowVersion) || !FuelPolicy.TokenVersion(request.TripRowVersion) ||
               !FuelPolicy.TokenVersion(request.AssignmentRowVersion) || !FuelPolicy.TokenVersion(request.BusRowVersion) || !FuelPolicy.TokenVersion(request.StationRowVersion))
                return ServiceResult<long>.Failure("","Reload the request and Station before approving.");
            DateTime utc=clock.UtcNow,now=clock.ToOperationalTime(utc);
            var item=repository.GetRequest(request.FuelRequestId,actor,false);
            var station=stations.GetStation(request.FuelStationId,actor);
            var errors=FuelPolicy.ValidateApproval(item,station,request,now);
            if(errors.Count>0) return ServiceResult<long>.Failure(errors);
            request.Note=string.IsNullOrWhiteSpace(request.Note)?null:request.Note.Trim();
            string token=FuelRedemptionToken.Create();
            try { return ServiceResult<long>.Success(repository.Approve(request,actor,FuelRedemptionToken.Hash(token),protector.Protect(token),now,utc),FuelPolicy.Warnings(item.Context,now)); }
            catch(FuelPersistenceException error) { return ServiceResult<long>.Failure("",error.Message); }
        }
        public ServiceResult<bool> Reject(RejectFuelRequest request,long actor)
        {
            if(request==null || !FuelPolicy.TokenVersion(request.RowVersion)) return ServiceResult<bool>.Failure("","Reload the request before rejecting.");
            if(string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length>500) return ServiceResult<bool>.Failure("Reason","Enter a rejection reason within 500 characters.");
            request.Reason=request.Reason.Trim();
            try { repository.Reject(request,actor,clock.UtcNow);return ServiceResult<bool>.Success(true); }
            catch(FuelPersistenceException error) { return ServiceResult<bool>.Failure("",error.Message); }
        }
        public ServiceResult<FuelRedemptionPreview> PreviewRedemption(string token,long stationId,long actor)
        {
            byte[] hash=FuelRedemptionToken.Hash((token??"").Trim());
            if(hash==null) return ServiceResult<FuelRedemptionPreview>.Failure("Token","Enter a valid Voucher token.");
            var voucher=repository.FindVoucher(hash,actor);var station=stations.GetStation(stationId,actor);
            var block=FuelPolicy.RedemptionBlock(voucher,station,clock.OperationalNow);
            if(block!=null) return ServiceResult<FuelRedemptionPreview>.Failure("",block);
            return ServiceResult<FuelRedemptionPreview>.Success(new FuelRedemptionPreview { Voucher=voucher,StationRowVersion=station.RowVersion,
                Fingerprint=FuelPolicy.Fingerprint(voucher,station),Warnings=FuelPolicy.Warnings(voucher.Request.Context,clock.OperationalNow) });
        }
        public ServiceResult<FuelRedemptionResult> Redeem(RedeemFuelVoucherRequest request,long actor)
        {
            byte[] hash=request==null?null:FuelRedemptionToken.Hash((request.Token??"").Trim());
            if(hash==null || !FuelPolicy.TokenVersion(request.VoucherRowVersion) || !FuelPolicy.TokenVersion(request.StationRowVersion) || string.IsNullOrEmpty(request.PreviewFingerprint))
                return ServiceResult<FuelRedemptionResult>.Failure("","Review the Voucher before confirming redemption.");
            DateTime utc=clock.UtcNow;
            try { return ServiceResult<FuelRedemptionResult>.Success(repository.Redeem(request,hash,actor,clock.ToOperationalTime(utc),utc)); }
            catch(FuelPersistenceException error) { return ServiceResult<FuelRedemptionResult>.Failure("",error.Message); }
        }
        public DateTime ToLocal(DateTime utc) { return clock.ToOperationalTime(utc); }
        public DateTime OperationalNow { get { return clock.OperationalNow; } }
    }
}
