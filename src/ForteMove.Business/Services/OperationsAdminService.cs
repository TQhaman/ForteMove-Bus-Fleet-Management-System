using System;
using System.Collections.Generic;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Operations;

namespace ForteMove.Business.Services
{
    public sealed class OperationsAdminService
    {
        private readonly ITripOperationsRepository repository;
        private readonly IClock clock;

        public OperationsAdminService(ITripOperationsRepository repository) : this(repository, new SystemClock()) { }
        public OperationsAdminService(ITripOperationsRepository repository, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository = repository; this.clock = clock;
        }

        public IList<CannotProceedDetails> GetCannotProceedReports(ExceptionQuery query) { return repository.GetCannotProceedReports(query ?? new ExceptionQuery()); }
        public CannotProceedDetails GetCannotProceedDetails(long id) { return id <= 0 ? null : repository.GetCannotProceedDetails(id); }
        public IList<DefectReportDetails> GetDefectReports(DefectQuery query) { return repository.GetDefectReports(query ?? new DefectQuery()); }
        public DefectReportDetails GetDefectDetails(long id) { return id <= 0 ? null : repository.GetDefectDetails(id); }

        public ServiceResult<bool> AllowProceed(ResolveCannotProceedRequest request, long actor)
        {
            return Execute(ValidateNote(request == null ? null : request.ResolutionNote, "ResolutionNote", "Record how the Trip may proceed."),
                delegate { request.ResolutionNote = request.ResolutionNote.Trim(); repository.AllowProceed(request, actor, clock.UtcNow); });
        }

        public ServiceResult<bool> CancelTrip(CancelTripRequest request, long actor)
        {
            IList<ValidationError> errors = ValidateNote(request == null ? null : request.Reason, "Reason", "Record why the Trip is being cancelled.");
            if (request == null || request.TripId <= 0) errors.Add(new ValidationError(string.Empty, "A valid Trip is required."));
            return Execute(errors, delegate { request.Reason = request.Reason.Trim(); repository.CancelTrip(request, actor, clock.UtcNow); });
        }

        public ServiceResult<bool> ReviewDefect(UpdateDefectRequest request, long actor)
        {
            return Execute(ValidateDefect(request), delegate { request.Note = request.Note.Trim(); repository.ReviewDefect(request, actor, clock.UtcNow); });
        }

        public ServiceResult<bool> ResolveDefect(UpdateDefectRequest request, long actor)
        {
            return Execute(ValidateDefect(request), delegate { request.Note = request.Note.Trim(); repository.ResolveDefect(request, actor, clock.UtcNow); });
        }

        public DateTime ToOperationalTime(DateTime utc) { return clock.ToOperationalTime(utc); }

        private static IList<ValidationError> ValidateDefect(UpdateDefectRequest request)
        {
            IList<ValidationError> errors = ValidateNote(request == null ? null : request.Note, "Note", "Record the review outcome.");
            if (request == null || request.BusDefectReportId <= 0) errors.Add(new ValidationError(string.Empty, "A valid defect report is required."));
            return errors;
        }

        private static IList<ValidationError> ValidateNote(string note, string field, string required)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (string.IsNullOrWhiteSpace(note)) errors.Add(new ValidationError(field, required));
            else if (note.Trim().Length > 1000) errors.Add(new ValidationError(field, "Keep the note within 1,000 characters."));
            return errors;
        }

        private static ServiceResult<bool> Execute(IList<ValidationError> errors, Action action)
        {
            if (errors.Count > 0) return ServiceResult<bool>.Failure(errors);
            try { action(); return ServiceResult<bool>.Success(true); }
            catch (TripOperationsPersistenceException ex) { return ServiceResult<bool>.Failure(string.Empty, ex.Message); }
        }
    }
}
