using System;
using System.Collections.Generic;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Fuel;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fuel;
namespace ForteMove.Business.Services
{
    public sealed class DriverFuelService
    {
        private readonly IFuelRepository repository;
        private readonly IFuelTokenProtector protector;
        private readonly IClock clock;
        public DriverFuelService(IFuelRepository repository,IFuelTokenProtector protector,IClock clock=null)
        { this.repository=repository;this.protector=protector;this.clock=clock??new SystemClock(); }
        public IList<FuelTripContext> GetEligibleTrips(long userId) { return repository.GetEligibleTrips(userId).Where(FuelPolicy.Eligible).ToList(); }
        public FuelTripContext GetTrip(long id,long userId) { return repository.GetDriverTrip(id,userId); }
        public IList<FuelRequestDetails> GetRequests(FuelQuery query,long userId) { return repository.GetRequests(query,userId,true); }
        public FuelRequestDetails GetRequest(long id,long userId) { return repository.GetRequest(id,userId,true); }
        public IList<FuelVoucherDetails> GetVouchers(FuelQuery query,long userId)
        {
            DateTime now=clock.OperationalNow;
            var items=repository.GetVouchers(query,userId,true);
            foreach(var item in items) item.DisplayState=FuelPolicy.DisplayState(item,now);
            return items;
        }
        public FuelVoucherDetails GetVoucher(long id,long userId)
        {
            var item=repository.GetVoucher(id,userId,true);
            if(item!=null) item.DisplayState=FuelPolicy.DisplayState(item,clock.OperationalNow);
            return item;
        }
        public ServiceResult<string> GetVoucherToken(long id,long userId)
        {
            var item=GetVoucher(id,userId);
            if(item==null || item.DisplayState!=FuelVoucherDisplayState.Active || !FuelPolicy.Eligible(item.Request.Context))
                return ServiceResult<string>.Failure("","A current, unexpired Voucher belonging to you is required.");
            try { return ServiceResult<string>.Success(protector.Unprotect(item.ProtectedToken)); }
            catch(System.Security.Cryptography.CryptographicException) { return ServiceResult<string>.Failure("","Voucher display is unavailable. Ask the Administrator to check the application's protection keys."); }
        }
        public ServiceResult<FuelSubmissionResult> Submit(SubmitFuelRequest request,long userId)
        {
            if(request==null || request.TripId<=0 || request.SubmissionToken==Guid.Empty ||
                !FuelPolicy.TokenVersion(request.TripRowVersion) || !FuelPolicy.TokenVersion(request.AssignmentRowVersion))
                return ServiceResult<FuelSubmissionResult>.Failure("","Reload the Trip before submitting.");
            if(string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Trim().Length>500)
                return ServiceResult<FuelSubmissionResult>.Failure("Justification","Enter an operational reason within 500 characters.");
            request.Justification=request.Justification.Trim();
            DateTime utc=clock.UtcNow;
            try { return ServiceResult<FuelSubmissionResult>.Success(repository.Submit(request,userId,clock.ToOperationalTime(utc),utc)); }
            catch(FuelPersistenceException error) { return ServiceResult<FuelSubmissionResult>.Failure("",error.Message); }
        }
        public DateTime ToLocal(DateTime utc) { return clock.ToOperationalTime(utc); }
    }
}
