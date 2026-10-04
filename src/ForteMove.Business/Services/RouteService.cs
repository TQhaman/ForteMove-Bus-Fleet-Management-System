using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Models.Common;
using ForteMove.Models.Routing;

namespace ForteMove.Business.Services
{
    public sealed class RouteService
    {
        private const int MaximumRouteNameLength = 150;
        private const int MaximumStopNameLength = 150;
        private const int MaximumAreaLength = 150;
        private const int MaximumSearchLength = 100;
        private const decimal MaximumDistanceKm = 999999.99m;
        private const decimal MaximumFare = 99999999.99m;

        private readonly IRouteRepository repository;

        public RouteService(IRouteRepository repository)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            this.repository = repository;
        }

        public RouteCreationOptions GetCreationOptions()
        {
            return new RouteCreationOptions
            {
                SuggestedRouteCode = IdentifierCodePolicy.FormatRouteCode(
                    repository.GetNextRouteCodeSequence()),
                SuggestedStopCode = IdentifierCodePolicy.FormatStopCode(
                    repository.GetNextStopCodeSequence()),
                ActiveStops = repository.GetActiveStops() ?? new List<StopOption>()
            };
        }

        public string GetSuggestedStopCode(int pendingNewStopCount)
        {
            if (pendingNewStopCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "pendingNewStopCount",
                    "The pending stop count cannot be negative.");
            }

            int nextSequence = repository.GetNextStopCodeSequence();
            int suggestedSequence = checked(nextSequence + pendingNewStopCount);
            return IdentifierCodePolicy.FormatStopCode(suggestedSequence);
        }

        public ServiceResult<RouteCreationResult> CreateRoute(
            CreateRouteRequest request,
            long actorUserAccountId)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null)
            {
                errors.Add(new ValidationError(string.Empty, "Route details are required."));
                return ServiceResult<RouteCreationResult>.Failure(errors);
            }

            string routeName = NormalizeDisplayText(request.RouteName);
            ValidateRequiredText(
                errors,
                "RouteName",
                "Route name",
                routeName,
                MaximumRouteNameLength);
            ValidateRouteNumbers(request, errors);

            if (actorUserAccountId <= 0)
            {
                errors.Add(new ValidationError(
                    string.Empty,
                    "A valid administrator is required to create a route."));
            }

            IList<RouteStopRequest> requestedStops = request.Stops ?? new List<RouteStopRequest>();
            if (requestedStops.Count < 2)
            {
                errors.Add(new ValidationError(
                    "Stops",
                    "A route requires at least two stops."));
            }

            IList<StopOption> activeStops = repository.GetActiveStops() ?? new List<StopOption>();
            IDictionary<long, StopOption> activeStopsById = activeStops
                .Where(stop => stop != null && stop.StopId > 0)
                .GroupBy(stop => stop.StopId)
                .ToDictionary(group => group.Key, group => group.First());
            ISet<string> activeStopKeys = new HashSet<string>(
                activeStops
                    .Where(stop => stop != null)
                    .Select(stop => BuildStopIdentityKey(stop.StopName, stop.Area)),
                StringComparer.Ordinal);
            ISet<long> selectedStopIds = new HashSet<long>();
            ISet<string> newStopKeys = new HashSet<string>(StringComparer.Ordinal);
            IList<RouteCreationStop> normalizedStops = new List<RouteCreationStop>();

            for (int index = 0; index < requestedStops.Count; index++)
            {
                RouteStopRequest requestedStop = requestedStops[index];
                int expectedOrder = index + 1;
                if (requestedStop == null)
                {
                    errors.Add(new ValidationError(
                        "Stops",
                        string.Format(
                            CultureInfo.CurrentCulture,
                            "Stop {0} is invalid.",
                            expectedOrder)));
                    continue;
                }

                if (requestedStop.StopOrder != expectedOrder)
                {
                    errors.Add(new ValidationError(
                        "Stops",
                        "Stop order must begin at 1 and remain continuous without gaps."));
                }

                bool hasExistingStop = requestedStop.ExistingStopId.HasValue;
                bool hasNewStop = requestedStop.NewStop != null;
                if (hasExistingStop == hasNewStop)
                {
                    errors.Add(new ValidationError(
                        "Stops",
                        string.Format(
                            CultureInfo.CurrentCulture,
                            "Stop {0} must be either one existing stop or one new stop.",
                            expectedOrder)));
                    continue;
                }

                RouteCreationStop normalizedStop = new RouteCreationStop
                {
                    StopOrder = expectedOrder,
                    EstimatedMinutesFromOrigin = requestedStop.EstimatedMinutesFromOrigin
                };

                if (hasExistingStop)
                {
                    long stopId = requestedStop.ExistingStopId.Value;
                    normalizedStop.ExistingStopId = stopId;
                    if (stopId <= 0 || !activeStopsById.ContainsKey(stopId))
                    {
                        errors.Add(new ValidationError(
                            "Stops",
                            string.Format(
                                CultureInfo.CurrentCulture,
                                "Stop {0} is no longer available. Select an active stop.",
                                expectedOrder)));
                    }
                    else if (!selectedStopIds.Add(stopId))
                    {
                        errors.Add(new ValidationError(
                            "Stops",
                            "A stop cannot appear more than once on the same route."));
                    }
                }
                else
                {
                    Stop normalizedNewStop = ValidateAndNormalizeNewStop(
                        requestedStop.NewStop,
                        expectedOrder,
                        errors);
                    normalizedStop.NewStop = normalizedNewStop;
                    if (normalizedNewStop != null)
                    {
                        string identityKey = BuildStopIdentityKey(
                            normalizedNewStop.NormalizedStopName,
                            normalizedNewStop.NormalizedArea);
                        if (activeStopKeys.Contains(identityKey))
                        {
                            errors.Add(new ValidationError(
                                "Stops",
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    "Stop {0} already exists. Select the existing active stop instead.",
                                    expectedOrder)));
                        }
                        else if (!newStopKeys.Add(identityKey))
                        {
                            errors.Add(new ValidationError(
                                "Stops",
                                "The same new stop cannot appear more than once on a route."));
                        }
                    }
                }

                normalizedStops.Add(normalizedStop);
            }

            ValidateEstimatedMinutes(normalizedStops, errors);

            if (errors.Count > 0)
            {
                return ServiceResult<RouteCreationResult>.Failure(errors);
            }

            RouteCreationAggregate aggregate = new RouteCreationAggregate
            {
                Route = new Route
                {
                    RouteName = routeName,
                    EstimatedDistanceKm = request.EstimatedDistanceKm.Value,
                    EstimatedDurationMinutes = request.EstimatedDurationMinutes.Value,
                    DefaultFare = request.DefaultFare.Value,
                    IsActive = true
                },
                Stops = normalizedStops
            };

            try
            {
                RouteCreationResult result = repository.CreateRoute(
                    aggregate,
                    actorUserAccountId);
                if (result == null || result.RouteId <= 0 || string.IsNullOrWhiteSpace(result.RouteCode))
                {
                    throw new InvalidOperationException(
                        "The repository did not return the created route identifier and code.");
                }

                return ServiceResult<RouteCreationResult>.Success(result);
            }
            catch (DuplicateStopException exception)
            {
                return DuplicateStopFailure(exception.Field);
            }
            catch (DuplicateRouteException)
            {
                return ServiceResult<RouteCreationResult>.Failure(
                    string.Empty,
                    "A unique route code could not be assigned. Please try again.");
            }
            catch (UnavailableRouteStopException)
            {
                return ServiceResult<RouteCreationResult>.Failure(
                    "Stops",
                    "A selected stop is no longer active. Review the route stops and try again.");
            }
        }

        public IList<RouteListItem> GetRouteList(RouteQuery query)
        {
            RouteQuery normalizedQuery = new RouteQuery();
            if (query != null)
            {
                string searchTerm = NormalizeDisplayText(query.SearchTerm);
                if (searchTerm != null && searchTerm.Length > MaximumSearchLength)
                {
                    searchTerm = searchTerm.Substring(0, MaximumSearchLength);
                }

                normalizedQuery.SearchTerm = searchTerm;
                normalizedQuery.IsActive = query.IsActive;
            }

            return repository.GetRouteList(normalizedQuery) ?? new List<RouteListItem>();
        }

        public RouteDetails GetRouteDetails(long routeId)
        {
            return routeId <= 0 ? null : repository.GetRouteDetails(routeId);
        }

        private static void ValidateRouteNumbers(
            CreateRouteRequest request,
            IList<ValidationError> errors)
        {
            if (!request.EstimatedDistanceKm.HasValue || request.EstimatedDistanceKm.Value <= 0)
            {
                errors.Add(new ValidationError(
                    "EstimatedDistanceKm",
                    "Estimated distance must be greater than zero."));
            }
            else if (request.EstimatedDistanceKm.Value > MaximumDistanceKm ||
                HasMoreThanDecimalPlaces(request.EstimatedDistanceKm.Value, 2))
            {
                errors.Add(new ValidationError(
                    "EstimatedDistanceKm",
                    "Estimated distance must fit the route register and use no more than two decimal places."));
            }

            if (!request.EstimatedDurationMinutes.HasValue ||
                request.EstimatedDurationMinutes.Value <= 0)
            {
                errors.Add(new ValidationError(
                    "EstimatedDurationMinutes",
                    "Estimated duration must be greater than zero."));
            }

            if (!request.DefaultFare.HasValue || request.DefaultFare.Value < 0)
            {
                errors.Add(new ValidationError(
                    "DefaultFare",
                    "Default fare is required and cannot be negative."));
            }
            else if (request.DefaultFare.Value > MaximumFare ||
                HasMoreThanDecimalPlaces(request.DefaultFare.Value, 2))
            {
                errors.Add(new ValidationError(
                    "DefaultFare",
                    "Default fare must fit the fare register and use no more than two decimal places."));
            }
        }

        private static Stop ValidateAndNormalizeNewStop(
            CreateStopRequest request,
            int stopOrder,
            IList<ValidationError> errors)
        {
            string stopName = NormalizeDisplayText(request == null ? null : request.StopName);
            string area = NormalizeDisplayText(request == null ? null : request.Area);
            int errorCountBeforeValidation = errors.Count;

            ValidateRequiredText(
                errors,
                "Stops",
                string.Format(CultureInfo.CurrentCulture, "Stop {0} name", stopOrder),
                stopName,
                MaximumStopNameLength);
            ValidateRequiredText(
                errors,
                "Stops",
                string.Format(CultureInfo.CurrentCulture, "Stop {0} area", stopOrder),
                area,
                MaximumAreaLength);

            decimal? latitude = request == null ? null : request.Latitude;
            decimal? longitude = request == null ? null : request.Longitude;
            if (latitude.HasValue != longitude.HasValue)
            {
                errors.Add(new ValidationError(
                    "Stops",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Stop {0} must include both latitude and longitude, or leave both empty.",
                        stopOrder)));
            }

            if (latitude.HasValue &&
                (latitude.Value < -90m || latitude.Value > 90m))
            {
                errors.Add(new ValidationError(
                    "Stops",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Stop {0} latitude must be between -90 and 90.",
                        stopOrder)));
            }
            else if (latitude.HasValue && HasMoreThanDecimalPlaces(latitude.Value, 6))
            {
                errors.Add(new ValidationError(
                    "Stops",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Stop {0} latitude may use no more than six decimal places.",
                        stopOrder)));
            }

            if (longitude.HasValue &&
                (longitude.Value < -180m || longitude.Value > 180m))
            {
                errors.Add(new ValidationError(
                    "Stops",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Stop {0} longitude must be between -180 and 180.",
                        stopOrder)));
            }
            else if (longitude.HasValue && HasMoreThanDecimalPlaces(longitude.Value, 6))
            {
                errors.Add(new ValidationError(
                    "Stops",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Stop {0} longitude may use no more than six decimal places.",
                        stopOrder)));
            }

            if (errors.Count > errorCountBeforeValidation)
            {
                return null;
            }

            return new Stop
            {
                StopName = stopName,
                NormalizedStopName = NormalizeIdentityText(stopName),
                Area = area,
                NormalizedArea = NormalizeIdentityText(area),
                Latitude = latitude,
                Longitude = longitude,
                IsActive = true
            };
        }

        private static void ValidateEstimatedMinutes(
            IList<RouteCreationStop> stops,
            IList<ValidationError> errors)
        {
            if (stops == null || stops.Count == 0)
            {
                return;
            }

            bool anyCaptured = stops.Any(stop =>
                stop != null && stop.EstimatedMinutesFromOrigin.HasValue);
            if (!anyCaptured)
            {
                return;
            }

            if (!stops[0].EstimatedMinutesFromOrigin.HasValue ||
                stops[0].EstimatedMinutesFromOrigin.Value != 0)
            {
                errors.Add(new ValidationError(
                    "Stops",
                    "The first stop must be 0 minutes from the origin when stop timing is captured."));
            }

            int? previousMinutes = null;
            foreach (RouteCreationStop stop in stops)
            {
                if (stop == null || !stop.EstimatedMinutesFromOrigin.HasValue)
                {
                    continue;
                }

                int minutes = stop.EstimatedMinutesFromOrigin.Value;
                if (minutes < 0)
                {
                    errors.Add(new ValidationError(
                        "Stops",
                        "Estimated minutes from origin cannot be negative."));
                }
                else if (previousMinutes.HasValue && minutes < previousMinutes.Value)
                {
                    errors.Add(new ValidationError(
                        "Stops",
                        "Estimated minutes from origin cannot decrease as stop order increases."));
                }

                previousMinutes = minutes;
            }
        }

        private static ServiceResult<RouteCreationResult> DuplicateStopFailure(
            DuplicateStopField field)
        {
            if (field == DuplicateStopField.NameAndArea)
            {
                return ServiceResult<RouteCreationResult>.Failure(
                    "Stops",
                    "An active stop with the same name and area already exists. Select that stop instead.");
            }

            return ServiceResult<RouteCreationResult>.Failure(
                string.Empty,
                "A unique stop code could not be assigned. Please try again.");
        }

        private static void ValidateRequiredText(
            IList<ValidationError> errors,
            string field,
            string label,
            string value,
            int maximumLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                errors.Add(new ValidationError(field, label + " is required."));
                return;
            }

            if (value.Length > maximumLength)
            {
                errors.Add(new ValidationError(
                    field,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} cannot exceed {1} characters.",
                        label,
                        maximumLength)));
            }
        }

        private static string BuildStopIdentityKey(string stopName, string area)
        {
            return NormalizeIdentityText(stopName) + "\u001F" + NormalizeIdentityText(area);
        }

        private static string NormalizeIdentityText(string value)
        {
            string displayText = NormalizeDisplayText(value);
            return displayText == null ? string.Empty : displayText.ToUpperInvariant();
        }

        private static string NormalizeDisplayText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            StringBuilder normalized = new StringBuilder(value.Length);
            bool previousWasWhitespace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace)
                    {
                        normalized.Append(' ');
                    }

                    previousWasWhitespace = true;
                }
                else
                {
                    normalized.Append(character);
                    previousWasWhitespace = false;
                }
            }

            return normalized.ToString();
        }

        private static bool HasMoreThanDecimalPlaces(decimal value, int decimalPlaces)
        {
            return value != decimal.Round(value, decimalPlaces, MidpointRounding.ToEven);
        }
    }
}
