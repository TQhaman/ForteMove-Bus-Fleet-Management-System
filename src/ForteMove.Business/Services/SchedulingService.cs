using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Scheduling;

namespace ForteMove.Business.Services
{
    public sealed class SchedulingService
    {
        private const int MaximumEffectiveDays = 366;
        private const int MaximumTripsPerVersion = 10000;
        private const int MaximumSearchLength = 100;

        private readonly ISchedulingRepository repository;
        private readonly IClock clock;

        public SchedulingService(ISchedulingRepository repository)
            : this(repository, new SystemClock())
        {
        }

        public SchedulingService(ISchedulingRepository repository, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository = repository;
            this.clock = clock;
        }

        public SchedulingCreationOptions GetCreationOptions()
        {
            SchedulingCreationOptions options = repository.GetCreationOptions() ??
                new SchedulingCreationOptions();
            options.SuggestedScheduleCode = IdentifierCodePolicy.FormatScheduleCode(
                repository.GetNextScheduleCodeSequence());
            return options;
        }

        public ServiceResult<TimeSpan> ValidateDepartureTimeDraft(
            TimeSpan departureTime,
            IEnumerable<TimeSpan> existingTimes)
        {
            if (departureTime < TimeSpan.Zero ||
                departureTime >= TimeSpan.FromDays(1) ||
                departureTime.Seconds != 0 ||
                departureTime.Milliseconds != 0)
            {
                return ServiceResult<TimeSpan>.Failure(
                    "DepartureTimes",
                    "Enter a valid departure time using hours and minutes.");
            }

            if ((existingTimes ?? Enumerable.Empty<TimeSpan>()).Contains(departureTime))
            {
                return ServiceResult<TimeSpan>.Failure(
                    "DepartureTimes",
                    "That departure time has already been added.");
            }

            return ServiceResult<TimeSpan>.Success(departureTime);
        }

        public ServiceResult<SchedulePreview> PreviewSchedule(CreateScheduleRequest request)
        {
            return BuildSchedulePreview(request, clock.OperationalNow, repository.GetCreationOptions());
        }

        public ServiceResult<ScheduleCreationResult> CreateSchedule(
            CreateScheduleRequest request,
            long actorUserAccountId)
        {
            if (actorUserAccountId <= 0)
            {
                return ServiceResult<ScheduleCreationResult>.Failure(
                    string.Empty,
                    "The signed-in administrator could not be identified.");
            }

            DateTime operationalNow = clock.OperationalNow;
            SchedulingCreationOptions options = repository.GetCreationOptions();
            ServiceResult<SchedulePreview> previewResult =
                BuildSchedulePreview(request, operationalNow, options);
            if (!previewResult.Succeeded)
            {
                return ServiceResult<ScheduleCreationResult>.Failure(previewResult.Errors);
            }

            SchedulePreview preview = previewResult.Value;
            if (request == null ||
                !FixedEquals(request.PreviewRequestFingerprint, preview.RequestFingerprint) ||
                !FixedEquals(request.PreviewOccurrenceSignature, preview.OccurrenceSignature))
            {
                return ServiceResult<ScheduleCreationResult>.Failure(
                    "Preview",
                    "The schedule preview has changed. Review the updated Trip count before creating the schedule.");
            }

            ScheduleRouteOption route = options.Routes.First(item => item.RouteId == request.RouteId.Value);
            ScheduleCreationAggregate aggregate = new ScheduleCreationAggregate
            {
                RouteId = route.RouteId,
                RouteRowVersion = route.RowVersion,
                EffectiveStartDate = request.EffectiveStartDate.Value.Date,
                EffectiveEndDate = request.EffectiveEndDate.Value.Date,
                PreferredBusCategoryId = request.PreferredBusCategoryId,
                ExpectedCapacity = request.ExpectedCapacity,
                OperatingDays = request.OperatingDays.Distinct().OrderBy(item => (byte)item).ToList(),
                DepartureTimes = request.DepartureTimes.Distinct().OrderBy(item => item).ToList(),
                Occurrences = preview.Occurrences
            };

            try
            {
                return ServiceResult<ScheduleCreationResult>.Success(
                    repository.CreateSchedule(aggregate, actorUserAccountId));
            }
            catch (SchedulingConflictException exception)
            {
                return ServiceResult<ScheduleCreationResult>.Failure("DepartureTimes", exception.Message);
            }
            catch (SchedulingConcurrencyException exception)
            {
                return ServiceResult<ScheduleCreationResult>.Failure(string.Empty, exception.Message);
            }
            catch (SchedulingReferenceException exception)
            {
                return ServiceResult<ScheduleCreationResult>.Failure("RouteId", exception.Message);
            }
        }

        public IList<ScheduleListItem> GetScheduleList(ScheduleQuery query)
        {
            query = query ?? new ScheduleQuery();
            query.SearchTerm = (query.SearchTerm ?? string.Empty).Trim();
            if (query.SearchTerm.Length > MaximumSearchLength)
            {
                query.SearchTerm = query.SearchTerm.Substring(0, MaximumSearchLength);
            }

            return repository.GetScheduleList(query, clock.OperationalNow) ??
                new List<ScheduleListItem>();
        }

        public ScheduleDetails GetScheduleDetails(long routeScheduleId)
        {
            if (routeScheduleId <= 0) return null;
            return repository.GetScheduleDetails(routeScheduleId, clock.OperationalNow);
        }

        public IList<TripListItem> GetTripList(TripQuery query)
        {
            TripQuery effective = query ?? new TripQuery();
            effective.OperationalNow = clock.OperationalNow;
            return repository.GetTripList(effective) ?? new List<TripListItem>();
        }

        public ScheduleChangeOptions GetChangeOptions(long routeScheduleId)
        {
            if (routeScheduleId <= 0) return null;
            return repository.GetChangeOptions(routeScheduleId, null);
        }

        public ServiceResult<ScheduleChangeImpact> PreviewScheduleChange(
            ChangeScheduleRequest request)
        {
            return BuildChangeImpact(request, clock.OperationalNow);
        }

        public ServiceResult<ScheduleChangeResult> ApplyScheduleChange(
            ChangeScheduleRequest request,
            long actorUserAccountId)
        {
            if (actorUserAccountId <= 0)
            {
                return ServiceResult<ScheduleChangeResult>.Failure(
                    string.Empty,
                    "The signed-in administrator could not be identified.");
            }

            DateTime operationalNow = clock.OperationalNow;
            ServiceResult<ScheduleChangeImpact> impactResult =
                BuildChangeImpact(request, operationalNow);
            if (!impactResult.Succeeded)
            {
                return ServiceResult<ScheduleChangeResult>.Failure(impactResult.Errors);
            }

            ScheduleChangeImpact impact = impactResult.Value;
            if (request == null ||
                !FixedEquals(request.PreviewRequestFingerprint, impact.RequestFingerprint) ||
                !FixedEquals(request.PreviewOccurrenceSignature, impact.OccurrenceSignature))
            {
                return ServiceResult<ScheduleChangeResult>.Failure(
                    "Preview",
                    "The change impact has changed. Review the updated impact before applying it.");
            }

            ScheduleChangeOptions options = repository.GetChangeOptions(
                request.RouteScheduleId,
                request.ChangeEffectiveDate.Value.Date);
            if (options == null)
            {
                return ServiceResult<ScheduleChangeResult>.Failure(
                    string.Empty,
                    "The schedule is no longer available.");
            }
            CreateScheduleRequest pattern = ToPatternRequest(request, options.RouteId);
            ServiceResult<SchedulePreview> generated = BuildSchedulePreview(
                pattern,
                operationalNow,
                ToCreationOptions(options));
            if (!generated.Succeeded)
            {
                return ServiceResult<ScheduleChangeResult>.Failure(generated.Errors);
            }

            ScheduleChangeAggregate aggregate = new ScheduleChangeAggregate
            {
                RouteScheduleId = options.RouteScheduleId,
                CurrentVersionId = options.RouteScheduleVersionId,
                RouteId = options.RouteId,
                EstimatedDurationMinutes = options.EstimatedDurationMinutes,
                ScheduleRowVersion = request.ScheduleRowVersion,
                VersionRowVersion = request.VersionRowVersion,
                NewVersionNumber = options.VersionNumber + 1,
                ChangeEffectiveDate = request.ChangeEffectiveDate.Value.Date,
                EffectiveEndDate = request.EffectiveEndDate.Value.Date,
                PreferredBusCategoryId = request.PreferredBusCategoryId,
                ExpectedCapacity = request.ExpectedCapacity,
                OperatingDays = request.OperatingDays.Distinct().OrderBy(item => (byte)item).ToList(),
                DepartureTimes = request.DepartureTimes.Distinct().OrderBy(item => item).ToList(),
                Occurrences = generated.Value.Occurrences,
                ExpectedFutureTripsToReplace = impact.FutureTripsToReplace,
                ExpectedProtectedTrips = impact.ProtectedTripsRequiringReview,
                ExpectedTicketProtectedTrips = impact.TicketProtectedTripCount,
                ExpectedPurchasedTickets = impact.PurchasedTicketCount
            };

            try
            {
                return ServiceResult<ScheduleChangeResult>.Success(
                    repository.ApplyScheduleChange(aggregate, actorUserAccountId));
            }
            catch (SchedulingConflictException exception)
            {
                return ServiceResult<ScheduleChangeResult>.Failure("DepartureTimes", exception.Message);
            }
            catch (SchedulingConcurrencyException exception)
            {
                return ServiceResult<ScheduleChangeResult>.Failure(string.Empty, exception.Message);
            }
            catch (SchedulingReferenceException exception)
            {
                return ServiceResult<ScheduleChangeResult>.Failure("RouteId", exception.Message);
            }
        }

        private ServiceResult<ScheduleChangeImpact> BuildChangeImpact(
            ChangeScheduleRequest request,
            DateTime operationalNow)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null || request.RouteScheduleId <= 0)
            {
                return ServiceResult<ScheduleChangeImpact>.Failure(
                    string.Empty,
                    "The schedule to change could not be identified.");
            }

            if (!request.ChangeEffectiveDate.HasValue)
            {
                errors.Add(new ValidationError("ChangeEffectiveDate", "Changes take effect date is required."));
                return ServiceResult<ScheduleChangeImpact>.Failure(errors);
            }

            DateTime changeDate = request.ChangeEffectiveDate.Value.Date;
            ScheduleChangeOptions options = repository.GetChangeOptions(
                request.RouteScheduleId,
                changeDate);
            if (options == null)
            {
                return ServiceResult<ScheduleChangeImpact>.Failure(
                    string.Empty,
                    "The schedule is no longer available.");
            }

            if (request.RouteScheduleVersionId != options.RouteScheduleVersionId)
            {
                errors.Add(new ValidationError(string.Empty, "The schedule has changed. Reload it before continuing."));
            }

            DateTime earliestChangeDate = operationalNow.Date.AddDays(1);
            if (changeDate < earliestChangeDate)
            {
                errors.Add(new ValidationError(
                    "ChangeEffectiveDate",
                    "Schedule changes must take effect tomorrow or later."));
            }

            if (changeDate < options.EffectiveStartDate.Date ||
                changeDate > options.EffectiveEndDate.Date)
            {
                errors.Add(new ValidationError(
                    "ChangeEffectiveDate",
                    "The change date must fall within the current schedule period."));
            }

            CreateScheduleRequest pattern = ToPatternRequest(request, options.RouteId);
            ServiceResult<SchedulePreview> generated = BuildSchedulePreview(
                pattern,
                operationalNow,
                ToCreationOptions(options));
            if (!generated.Succeeded)
            {
                foreach (ValidationError error in generated.Errors) errors.Add(error);
            }

            if (errors.Count > 0)
            {
                return ServiceResult<ScheduleChangeImpact>.Failure(errors);
            }

            IList<ExistingScheduleTrip> protectedTrips = options.ExistingTripsFromCutover
                .Where(item => !item.IsUntouched)
                .ToList();
            HashSet<string> protectedOccurrences = new HashSet<string>(
                protectedTrips.Select(item => OccurrenceKey(item.ServiceDate, item.ScheduledDepartureTime)),
                StringComparer.Ordinal);
            int protectedCollisions = generated.Value.Occurrences.Count(
                item => protectedOccurrences.Contains(
                    OccurrenceKey(item.ServiceDate, item.ScheduledDepartureTime)));

            string requestFingerprint = HashText(
                request.RouteScheduleId.ToString(CultureInfo.InvariantCulture) + "|" +
                BuildRequestFingerprint(pattern));
            string occurrenceSignature = generated.Value.OccurrenceSignature;

            return ServiceResult<ScheduleChangeImpact>.Success(new ScheduleChangeImpact
            {
                EvaluatedAtLocal = operationalNow,
                FutureTripsToReplace = options.ExistingTripsFromCutover.Count(item => item.IsUntouched),
                NewTripsToGenerate = generated.Value.TripCount - protectedCollisions,
                ProtectedTripsRequiringReview = protectedTrips.Count,
                TicketProtectedTripCount = protectedTrips.Count(item => item.HasTicketHistory),
                PurchasedTicketCount = protectedTrips.Sum(item => item.PurchasedTicketCount),
                HistoricalTripsAffected = 0,
                RequestFingerprint = requestFingerprint,
                OccurrenceSignature = occurrenceSignature
            });
        }

        private ServiceResult<SchedulePreview> BuildSchedulePreview(
            CreateScheduleRequest request,
            DateTime operationalNow,
            SchedulingCreationOptions options)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null)
            {
                return ServiceResult<SchedulePreview>.Failure(string.Empty, "Schedule details are required.");
            }

            options = options ?? new SchedulingCreationOptions();
            ScheduleRouteOption route = null;
            if (!request.RouteId.HasValue || request.RouteId.Value <= 0)
            {
                errors.Add(new ValidationError("RouteId", "Select an active Route."));
            }
            else
            {
                route = options.Routes.FirstOrDefault(item => item.RouteId == request.RouteId.Value);
                if (route == null)
                {
                    errors.Add(new ValidationError("RouteId", "Select an active Route."));
                }
            }

            IList<OperatingDay> days = request.OperatingDays == null
                ? new List<OperatingDay>()
                : request.OperatingDays.ToList();
            if (days.Count == 0)
            {
                errors.Add(new ValidationError("OperatingDays", "Select at least one operating day."));
            }
            else if (days.Any(item => (byte)item > 6) || days.Distinct().Count() != days.Count)
            {
                errors.Add(new ValidationError("OperatingDays", "Operating days must be valid and unique."));
            }

            IList<TimeSpan> times = request.DepartureTimes == null
                ? new List<TimeSpan>()
                : request.DepartureTimes.ToList();
            if (times.Count == 0)
            {
                errors.Add(new ValidationError("DepartureTimes", "Add at least one departure time."));
            }
            else
            {
                if (times.Any(item => item < TimeSpan.Zero || item >= TimeSpan.FromDays(1) || item.Seconds != 0 || item.Milliseconds != 0))
                {
                    errors.Add(new ValidationError("DepartureTimes", "Departure times must use valid minute precision."));
                }
                if (times.Distinct().Count() != times.Count)
                {
                    errors.Add(new ValidationError("DepartureTimes", "Departure times must be unique."));
                }
            }

            if (!request.EffectiveStartDate.HasValue)
                errors.Add(new ValidationError("EffectiveStartDate", "Effective start date is required."));
            if (!request.EffectiveEndDate.HasValue)
                errors.Add(new ValidationError("EffectiveEndDate", "Effective end date is required."));

            if (request.EffectiveStartDate.HasValue && request.EffectiveEndDate.HasValue)
            {
                DateTime start = request.EffectiveStartDate.Value.Date;
                DateTime end = request.EffectiveEndDate.Value.Date;
                if (start < operationalNow.Date)
                    errors.Add(new ValidationError("EffectiveStartDate", "Effective start date cannot be in the past."));
                if (end < start)
                    errors.Add(new ValidationError("EffectiveEndDate", "Effective end date cannot precede the start date."));
                else if ((end - start).Days + 1 > MaximumEffectiveDays)
                    errors.Add(new ValidationError("EffectiveEndDate", "A schedule period cannot exceed 366 calendar days."));
            }

            if (request.ExpectedCapacity.HasValue && request.ExpectedCapacity.Value <= 0)
                errors.Add(new ValidationError("ExpectedCapacity", "Expected capacity must be greater than zero."));

            if (request.PreferredBusCategoryId.HasValue &&
                !options.BusCategories.Any(item => item.Id == request.PreferredBusCategoryId.Value))
            {
                errors.Add(new ValidationError("PreferredBusCategoryId", "Select an active preferred bus category."));
            }

            if (errors.Count > 0)
                return ServiceResult<SchedulePreview>.Failure(errors);

            List<ScheduleOccurrence> occurrences = GenerateOccurrences(
                request.EffectiveStartDate.Value.Date,
                request.EffectiveEndDate.Value.Date,
                new HashSet<OperatingDay>(days),
                times.Distinct().OrderBy(item => item).ToList(),
                route.EstimatedDurationMinutes,
                operationalNow,
                errors);
            if (occurrences.Count == 0)
            {
                errors.Add(new ValidationError(
                    "DepartureTimes",
                    "This pattern has no future departures in the selected period."));
            }
            if (errors.Count > 0)
                return ServiceResult<SchedulePreview>.Failure(errors);

            return ServiceResult<SchedulePreview>.Success(new SchedulePreview
            {
                EvaluatedAtLocal = operationalNow,
                TripCount = occurrences.Count,
                FirstDepartureLocal = occurrences[0].ScheduledDepartureLocal,
                LastDepartureLocal = occurrences[occurrences.Count - 1].ScheduledDepartureLocal,
                RequestFingerprint = BuildRequestFingerprint(request),
                OccurrenceSignature = BuildOccurrenceSignature(occurrences),
                Occurrences = occurrences
            });
        }

        private static List<ScheduleOccurrence> GenerateOccurrences(
            DateTime startDate,
            DateTime endDate,
            HashSet<OperatingDay> days,
            IList<TimeSpan> times,
            int durationMinutes,
            DateTime operationalNow,
            IList<ValidationError> errors)
        {
            List<ScheduleOccurrence> occurrences = new List<ScheduleOccurrence>();
            for (DateTime date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (!days.Contains((OperatingDay)date.DayOfWeek)) continue;
                foreach (TimeSpan time in times)
                {
                    DateTime departure = date.Add(time);
                    if (departure <= operationalNow) continue;
                    occurrences.Add(new ScheduleOccurrence
                    {
                        ServiceDate = date,
                        ScheduledDepartureTime = time,
                        EstimatedDurationMinutesSnapshot = durationMinutes,
                        ExpectedFinishLocal = departure.AddMinutes(durationMinutes)
                    });
                    if (occurrences.Count > MaximumTripsPerVersion)
                    {
                        errors.Add(new ValidationError(
                            "EffectiveEndDate",
                            "This schedule would generate more than 10,000 Trips. Shorten the period or reduce departure times."));
                        return occurrences;
                    }
                }
            }
            return occurrences;
        }

        private static CreateScheduleRequest ToPatternRequest(
            ChangeScheduleRequest request,
            long routeId)
        {
            return new CreateScheduleRequest
            {
                RouteId = routeId,
                OperatingDays = request.OperatingDays,
                DepartureTimes = request.DepartureTimes,
                EffectiveStartDate = request.ChangeEffectiveDate,
                EffectiveEndDate = request.EffectiveEndDate,
                PreferredBusCategoryId = request.PreferredBusCategoryId,
                ExpectedCapacity = request.ExpectedCapacity
            };
        }

        private static SchedulingCreationOptions ToCreationOptions(ScheduleChangeOptions options)
        {
            SchedulingCreationOptions result = new SchedulingCreationOptions
            {
                BusCategories = options.BusCategories
            };
            if (options.RouteIsActive)
            {
                result.Routes.Add(new ScheduleRouteOption
                {
                    RouteId = options.RouteId,
                    RouteCode = options.RouteCode,
                    RouteName = options.RouteName,
                    EstimatedDurationMinutes = options.EstimatedDurationMinutes
                });
            }
            return result;
        }

        private static string BuildRequestFingerprint(CreateScheduleRequest request)
        {
            string days = string.Join(",", (request.OperatingDays ?? new List<OperatingDay>())
                .Distinct().OrderBy(item => (byte)item).Select(item => ((byte)item).ToString(CultureInfo.InvariantCulture)));
            string times = string.Join(",", (request.DepartureTimes ?? new List<TimeSpan>())
                .Distinct().OrderBy(item => item).Select(item => item.ToString("c", CultureInfo.InvariantCulture)));
            string canonical = string.Join("|", new[]
            {
                request.RouteId.HasValue ? request.RouteId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                request.EffectiveStartDate.HasValue ? request.EffectiveStartDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty,
                request.EffectiveEndDate.HasValue ? request.EffectiveEndDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty,
                request.PreferredBusCategoryId.HasValue ? request.PreferredBusCategoryId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                request.ExpectedCapacity.HasValue ? request.ExpectedCapacity.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                days,
                times
            });
            return HashText(canonical);
        }

        private static string BuildOccurrenceSignature(IEnumerable<ScheduleOccurrence> occurrences)
        {
            return HashText(string.Join("|", occurrences.Select(item =>
                OccurrenceKey(item.ServiceDate, item.ScheduledDepartureTime))));
        }

        private static string OccurrenceKey(DateTime date, TimeSpan time)
        {
            return date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T" +
                time.ToString("c", CultureInfo.InvariantCulture);
        }

        private static string HashText(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static bool FixedEquals(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
            byte[] leftBytes;
            byte[] rightBytes;
            try
            {
                leftBytes = Convert.FromBase64String(Convert.ToBase64String(Encoding.UTF8.GetBytes(left)));
                rightBytes = Convert.FromBase64String(Convert.ToBase64String(Encoding.UTF8.GetBytes(right)));
            }
            catch (FormatException)
            {
                return false;
            }
            int difference = leftBytes.Length ^ rightBytes.Length;
            int maximum = Math.Max(leftBytes.Length, rightBytes.Length);
            for (int index = 0; index < maximum; index++)
            {
                byte leftValue = index < leftBytes.Length ? leftBytes[index] : (byte)0;
                byte rightValue = index < rightBytes.Length ? rightBytes[index] : (byte)0;
                difference |= leftValue ^ rightValue;
            }
            return difference == 0;
        }
    }
}
