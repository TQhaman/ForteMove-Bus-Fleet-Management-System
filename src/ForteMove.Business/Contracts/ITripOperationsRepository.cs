using System;
using System.Collections.Generic;
using ForteMove.Models.Operations;

namespace ForteMove.Business.Contracts
{
    public interface ITripOperationsRepository
    {
        IList<DriverTripListItem> GetDriverTrips(long userAccountId, DriverTripBucket bucket, DateTime operationalNow);
        DriverTripDetails GetDriverTripDetails(long userAccountId, long tripId, DateTime operationalNow);
        void ConfirmReadiness(ConfirmReadinessRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);
        void StartTrip(TripOperationRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);
        void ReportDelay(ReportDelayRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);
        void ResumeTrip(TripOperationRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);
        void ReportCannotProceed(ReportCannotProceedRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);
        DefectReportResult ReportDefect(ReportDefectRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);
        void CompleteTrip(CompleteTripRequest request, long userAccountId, DateTime operationalNow, DateTime utcNow);

        IList<CannotProceedDetails> GetCannotProceedReports(ExceptionQuery query);
        CannotProceedDetails GetCannotProceedDetails(long reportId);
        void AllowProceed(ResolveCannotProceedRequest request, long actorUserAccountId, DateTime utcNow);
        void CancelTrip(CancelTripRequest request, long actorUserAccountId, DateTime utcNow);
        IList<DefectReportDetails> GetDefectReports(DefectQuery query);
        DefectReportDetails GetDefectDetails(long reportId);
        void ReviewDefect(UpdateDefectRequest request, long actorUserAccountId, DateTime utcNow);
        void ResolveDefect(UpdateDefectRequest request, long actorUserAccountId, DateTime utcNow);
    }
}
