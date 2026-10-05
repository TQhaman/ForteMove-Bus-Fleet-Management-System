using System;
using System.Collections.Generic;
using ForteMove.Models.Fuel;
namespace ForteMove.Business.Contracts
{
    public interface IFuelRepository
    {
        IList<FuelTripContext> GetEligibleTrips(long driverUserId);
        FuelTripContext GetDriverTrip(long tripId,long driverUserId);
        IList<FuelRequestDetails> GetRequests(FuelQuery query,long userId,bool driver);
        FuelRequestDetails GetRequest(long id,long userId,bool driver);
        FuelSubmissionResult Submit(SubmitFuelRequest request,long userId,DateTime now,DateTime utc);
        long Approve(ApproveFuelRequest request,long actor,byte[] tokenHash,byte[] protectedToken,DateTime now,DateTime utc);
        void Reject(RejectFuelRequest request,long actor,DateTime utc);
        IList<FuelVoucherDetails> GetVouchers(FuelQuery query,long userId,bool driver);
        FuelVoucherDetails GetVoucher(long id,long userId,bool driver);
        FuelVoucherDetails FindVoucher(byte[] tokenHash,long actor);
        FuelRedemptionResult Redeem(RedeemFuelVoucherRequest request,byte[] tokenHash,long actor,DateTime now,DateTime utc);
        IList<FuelTransactionDetails> GetTransactions(FuelQuery query,long actor);
        FuelTransactionDetails GetTransaction(long id,long actor);
    }
}

