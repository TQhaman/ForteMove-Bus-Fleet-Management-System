using System;
using System.Collections.Generic;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Operations;

namespace ForteMove.Business.Services
{
    public sealed class DriverOperationsService
    {
        private readonly ITripOperationsRepository repository;
        private readonly IClock clock;

        public DriverOperationsService(ITripOperationsRepository repository)
            : this(repository, new SystemClock()) { }

        public DriverOperationsService(ITripOperationsRepository repository, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository = repository;
            this.clock = clock;
        }

        public IList<DriverTripListItem> GetToday(long userAccountId)
        {
            return repository.GetDriverTrips(userAccountId, DriverTripBucket.Today, clock.OperationalNow);
        }

        public IList<DriverTripListItem> GetUpcoming(long userAccountId)
        {
            return repository.GetDriverTrips(userAccountId, DriverTripBucket.Upcoming, clock.OperationalNow);
        }

        public IList<DriverTripListItem> GetHistory(long userAccountId)
        {
            return repository.GetDriverTrips(userAccountId, DriverTripBucket.History, clock.OperationalNow);
        }

        public DriverTripDetails GetTripDetails(long userAccountId, long tripId)
        {
            return tripId <= 0 ? null : repository.GetDriverTripDetails(userAccountId, tripId, clock.OperationalNow);
        }

        public ServiceResult<bool> ConfirmReadiness(ConfirmReadinessRequest request, long userAccountId)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null || request.TripId <= 0) errors.Add(new ValidationError(string.Empty, "A valid Trip is required."));
            else
            {
                if (!request.ExteriorConditionChecked || !request.TyresSafeChecked || !request.LightsIndicatorsChecked ||
                    !request.NoCriticalDashboardWarningsChecked || !request.DoorsOperationalChecked ||
                    !request.EmergencyEquipmentPresentChecked || !request.NoBlockingNewDefectChecked)
                    errors.Add(new ValidationError("Checklist", "Confirm every pre-trip safety check before marking the Trip ready."));
                if (!request.StartOdometerKilometres.HasValue)
                    errors.Add(new ValidationError("StartOdometerKilometres", "Enter the starting odometer."));
                else if (request.StartOdometerKilometres.Value < 0)
                    errors.Add(new ValidationError("StartOdometerKilometres", "The starting odometer cannot be negative."));
            }
            return Execute(errors, delegate { repository.ConfirmReadiness(request, userAccountId, clock.OperationalNow, clock.UtcNow); });
        }

        public ServiceResult<bool> StartTrip(TripOperationRequest request, long userAccountId)
        {
            return Execute(ValidateAction(request), delegate { repository.StartTrip(request, userAccountId, clock.OperationalNow, clock.UtcNow); });
        }

        public ServiceResult<bool> ReportDelay(ReportDelayRequest request, long userAccountId)
        {
            IList<ValidationError> errors = ValidateAction(request);
            if (request != null)
            {
                if (string.IsNullOrWhiteSpace(request.Reason)) errors.Add(new ValidationError("Reason", "Explain the delay."));
                else if (request.Reason.Trim().Length > 500) errors.Add(new ValidationError("Reason", "Keep the delay reason within 500 characters."));
                if (request.EstimatedDelayMinutes.HasValue && request.EstimatedDelayMinutes.Value <= 0)
                    errors.Add(new ValidationError("EstimatedDelayMinutes", "Estimated delay must be greater than zero."));
            }
            return Execute(errors, delegate { request.Reason = request.Reason.Trim(); repository.ReportDelay(request, userAccountId, clock.OperationalNow, clock.UtcNow); });
        }

        public ServiceResult<bool> ResumeTrip(TripOperationRequest request, long userAccountId)
        {
            return Execute(ValidateAction(request), delegate { repository.ResumeTrip(request, userAccountId, clock.OperationalNow, clock.UtcNow); });
        }

        public ServiceResult<bool> ReportCannotProceed(ReportCannotProceedRequest request, long userAccountId)
        {
            IList<ValidationError> errors = ValidateAction(request);
            if (request != null)
            {
                if (string.IsNullOrWhiteSpace(request.Reason)) errors.Add(new ValidationError("Reason", "Explain why the Trip cannot proceed."));
                else if (request.Reason.Trim().Length > 500) errors.Add(new ValidationError("Reason", "Keep the reason within 500 characters."));
                if (!string.IsNullOrWhiteSpace(request.Note) && request.Note.Trim().Length > 1000) errors.Add(new ValidationError("Note", "Keep the note within 1,000 characters."));
            }
            return Execute(errors, delegate { request.Reason = request.Reason.Trim(); request.Note = TrimToNull(request.Note); repository.ReportCannotProceed(request, userAccountId, clock.OperationalNow, clock.UtcNow); });
        }

        public ServiceResult<DefectReportResult> ReportDefect(ReportDefectRequest request, long userAccountId)
        {
            IList<ValidationError> errors = ValidateAction(request);
            if (request != null)
            {
                if (!request.Category.HasValue) errors.Add(new ValidationError("Category", "Select a defect category."));
                if (!request.Severity.HasValue) errors.Add(new ValidationError("Severity", "Select a defect severity."));
                if (string.IsNullOrWhiteSpace(request.Description)) errors.Add(new ValidationError("Description", "Describe the defect."));
                else if (request.Description.Trim().Length > 1000) errors.Add(new ValidationError("Description", "Keep the description within 1,000 characters."));
            }
            if (errors.Count > 0) return ServiceResult<DefectReportResult>.Failure(errors);
            try
            {
                request.Description = request.Description.Trim();
                return ServiceResult<DefectReportResult>.Success(repository.ReportDefect(request, userAccountId, clock.OperationalNow, clock.UtcNow));
            }
            catch (TripOperationsPersistenceException ex) { return ServiceResult<DefectReportResult>.Failure(string.Empty, ex.Message); }
        }

        public ServiceResult<bool> CompleteTrip(CompleteTripRequest request, long userAccountId)
        {
            IList<ValidationError> errors = ValidateAction(request);
            if (request != null)
            {
                if (!request.EndOdometerKilometres.HasValue) errors.Add(new ValidationError("EndOdometerKilometres", "Enter the ending odometer."));
                else if (request.EndOdometerKilometres.Value < 0) errors.Add(new ValidationError("EndOdometerKilometres", "The ending odometer cannot be negative."));
                if (!string.IsNullOrWhiteSpace(request.CompletionNote) && request.CompletionNote.Trim().Length > 1000) errors.Add(new ValidationError("CompletionNote", "Keep the completion note within 1,000 characters."));
            }
            return Execute(errors, delegate { request.CompletionNote = TrimToNull(request.CompletionNote); repository.CompleteTrip(request, userAccountId, clock.OperationalNow, clock.UtcNow); });
        }

        public DateTime ToOperationalTime(DateTime utc) { return clock.ToOperationalTime(utc); }

        private static IList<ValidationError> ValidateAction(TripOperationRequest request)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null || request.TripId <= 0) errors.Add(new ValidationError(string.Empty, "A valid Trip is required."));
            if (request != null && (request.TripRowVersion == null || request.AssignmentRowVersion == null)) errors.Add(new ValidationError(string.Empty, "This Trip view is stale. Reload it and try again."));
            return errors;
        }

        private static ServiceResult<bool> Execute(IList<ValidationError> errors, Action action)
        {
            if (errors.Count > 0) return ServiceResult<bool>.Failure(errors);
            try { action(); return ServiceResult<bool>.Success(true); }
            catch (TripOperationsPersistenceException ex) { return ServiceResult<bool>.Failure(string.Empty, ex.Message); }
        }

        private static string TrimToNull(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    }
}
