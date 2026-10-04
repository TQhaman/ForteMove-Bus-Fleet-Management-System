using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fleet;

namespace ForteMove.Business.Services
{
    public sealed class BusService
    {
        private const int MinimumManufactureYear = 1950;
        private const int MaximumPassengerCapacity = 200;
        private const int MaximumSearchLength = 100;
        private const decimal MaximumEnergyCapacity = 99999999.99m;
        private const decimal MaximumOdometerKilometres = 99999999999.9m;

        private readonly IBusRepository repository;
        private readonly IClock clock;

        public BusService(IBusRepository repository)
            : this(repository, new SystemClock())
        {
        }

        public BusService(IBusRepository repository, IClock clock)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            if (clock == null)
            {
                throw new ArgumentNullException("clock");
            }

            this.repository = repository;
            this.clock = clock;
        }

        public BusRegistrationOptions GetRegistrationOptions()
        {
            return new BusRegistrationOptions
            {
                Categories = repository.GetActiveBusCategories() ?? new List<LookupOption>(),
                PropulsionTypes = repository.GetActivePropulsionTypes() ?? new List<LookupOption>(),
                SuggestedFleetNumber = IdentifierCodePolicy.FormatFleetNumber(
                    repository.GetNextFleetNumberSequence())
            };
        }

        public ServiceResult<BusRegistrationResult> RegisterBus(
            RegisterBusRequest request,
            long actorUserAccountId)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null)
            {
                errors.Add(new ValidationError(string.Empty, "Bus details are required."));
                return ServiceResult<BusRegistrationResult>.Failure(errors);
            }

            string fleetNumber = NormalizeIdentifier(request.FleetNumber);
            string registrationNumber = NormalizeIdentifier(request.RegistrationNumber);
            string vin = NormalizeIdentifier(request.Vin);
            string make = NormalizeText(request.Make);
            string model = NormalizeText(request.Model);

            ValidateRequiredText(errors, "FleetNumber", "Fleet number", fleetNumber, 30);
            ValidateRequiredText(errors, "RegistrationNumber", "Registration number", registrationNumber, 30);
            ValidateRequiredText(errors, "Vin", "VIN", vin, 50);
            ValidateRequiredText(errors, "Make", "Make", make, 100);
            ValidateRequiredText(errors, "Model", "Model", model, 100);

            int maximumManufactureYear = clock.Today.Year + 1;
            if (!request.ManufactureYear.HasValue ||
                request.ManufactureYear.Value < MinimumManufactureYear ||
                request.ManufactureYear.Value > maximumManufactureYear)
            {
                errors.Add(new ValidationError(
                    "ManufactureYear",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Manufacture year must be between {0} and {1}.",
                        MinimumManufactureYear,
                        maximumManufactureYear)));
            }

            if (!request.PassengerCapacity.HasValue ||
                request.PassengerCapacity.Value < 1 ||
                request.PassengerCapacity.Value > MaximumPassengerCapacity)
            {
                errors.Add(new ValidationError(
                    "PassengerCapacity",
                    "Passenger capacity must be between 1 and 200."));
            }

            if (!request.OdometerKilometres.HasValue || request.OdometerKilometres.Value < 0)
            {
                errors.Add(new ValidationError(
                    "OdometerKilometres",
                    "Odometer reading must be zero or greater."));
            }
            else if (request.OdometerKilometres.Value > MaximumOdometerKilometres ||
                HasMoreThanDecimalPlaces(request.OdometerKilometres.Value, 1))
            {
                errors.Add(new ValidationError(
                    "OdometerKilometres",
                    "Odometer reading must fit the fleet register and use no more than one decimal place."));
            }

            ValidateRequiredDate(errors, "LicenceExpiryDate", "Licence expiry date", request.LicenceExpiryDate);
            ValidateRequiredDate(errors, "RoadworthyExpiryDate", "Roadworthy expiry date", request.RoadworthyExpiryDate);
            ValidateRequiredDate(errors, "InsuranceExpiryDate", "Insurance expiry date", request.InsuranceExpiryDate);

            BusRegistrationOptions options = GetRegistrationOptions();
            LookupOption selectedCategory = FindOption(options.Categories, request.BusCategoryId);
            if (selectedCategory == null)
            {
                errors.Add(new ValidationError("BusCategoryId", "Select an active bus category."));
            }

            LookupOption selectedPropulsion = FindOption(options.PropulsionTypes, request.PropulsionTypeId);
            if (selectedPropulsion == null)
            {
                errors.Add(new ValidationError("PropulsionTypeId", "Select an active propulsion type."));
            }
            else if (!IsKnownPropulsion(selectedPropulsion.Code))
            {
                errors.Add(new ValidationError("PropulsionTypeId", "Select a supported propulsion type."));
            }

            ValidateEnergyCapacities(errors, request, selectedPropulsion);

            if (actorUserAccountId <= 0)
            {
                errors.Add(new ValidationError(string.Empty, "A valid administrator is required to register a bus."));
            }

            if (errors.Count > 0)
            {
                return ServiceResult<BusRegistrationResult>.Failure(errors);
            }

            IList<string> expiredDocuments = GetExpiredDocuments(request, clock.Today);
            BusOperationalState baseState = expiredDocuments.Count == 0
                ? BusOperationalState.Operational
                : BusOperationalState.OutOfService;

            string propulsionCode = selectedPropulsion.Code == null
                ? string.Empty
                : selectedPropulsion.Code.Trim();

            Bus bus = new Bus
            {
                BusCategoryId = selectedCategory.Id,
                PropulsionTypeId = selectedPropulsion.Id,
                FleetNumber = fleetNumber,
                RegistrationNumber = registrationNumber,
                Vin = vin,
                Make = make,
                Model = model,
                ManufactureYear = request.ManufactureYear.Value,
                PassengerCapacity = request.PassengerCapacity.Value,
                FuelTankCapacityLitres = UsesFuel(propulsionCode) ? request.FuelTankCapacityLitres : null,
                BatteryCapacityKwh = UsesBattery(propulsionCode) ? request.BatteryCapacityKwh : null,
                OdometerKilometres = request.OdometerKilometres.Value,
                LicenceExpiryDate = request.LicenceExpiryDate.Value.Date,
                RoadworthyExpiryDate = request.RoadworthyExpiryDate.Value.Date,
                InsuranceExpiryDate = request.InsuranceExpiryDate.Value.Date,
                BaseOperationalState = baseState
            };

            try
            {
                long busId = repository.RegisterBus(bus, actorUserAccountId);
                BusRegistrationResult result = new BusRegistrationResult
                {
                    BusId = busId,
                    FleetNumber = fleetNumber,
                    BaseOperationalState = baseState
                };

                if (expiredDocuments.Count == 0)
                {
                    return ServiceResult<BusRegistrationResult>.Success(result);
                }

                string warning = string.Format(
                    CultureInfo.CurrentCulture,
                    "The bus was registered Out of Service because the following compliance document(s) have expired: {0}.",
                    string.Join(", ", expiredDocuments));
                return ServiceResult<BusRegistrationResult>.Success(result, new[] { warning });
            }
            catch (DuplicateBusException duplicateException)
            {
                return DuplicateFailure(duplicateException.Field);
            }
        }

        public IList<BusListItem> GetFleetList(FleetQuery query)
        {
            FleetQuery normalizedQuery = new FleetQuery();
            if (query != null)
            {
                string searchTerm = NormalizeText(query.SearchTerm);
                if (searchTerm != null && searchTerm.Length > MaximumSearchLength)
                {
                    searchTerm = searchTerm.Substring(0, MaximumSearchLength);
                }

                normalizedQuery.SearchTerm = searchTerm;
                normalizedQuery.BaseOperationalState = IsDefinedState(query.BaseOperationalState)
                    ? query.BaseOperationalState
                    : null;
            }

            IList<BusListItem> fleet = repository.GetFleetList(normalizedQuery) ?? new List<BusListItem>();
            DateTime today = clock.Today.Date;
            foreach (BusListItem bus in fleet.Where(item => item != null))
            {
                bus.ComplianceStatus = bus.LicenceExpiryDate.Date < today ||
                    bus.RoadworthyExpiryDate.Date < today ||
                    bus.InsuranceExpiryDate.Date < today
                    ? BusComplianceStatus.Expired
                    : BusComplianceStatus.Compliant;
            }

            return fleet;
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

        private static void ValidateRequiredDate(
            IList<ValidationError> errors,
            string field,
            string label,
            DateTime? value)
        {
            if (!value.HasValue)
            {
                errors.Add(new ValidationError(field, label + " is required."));
            }
        }

        private static void ValidateEnergyCapacities(
            IList<ValidationError> errors,
            RegisterBusRequest request,
            LookupOption selectedPropulsion)
        {
            if (request.FuelTankCapacityLitres.HasValue && request.FuelTankCapacityLitres.Value <= 0)
            {
                errors.Add(new ValidationError(
                    "FuelTankCapacityLitres",
                    "Fuel-tank capacity must be greater than zero."));
            }
            else if (request.FuelTankCapacityLitres.HasValue &&
                (request.FuelTankCapacityLitres.Value > MaximumEnergyCapacity ||
                 HasMoreThanDecimalPlaces(request.FuelTankCapacityLitres.Value, 2)))
            {
                errors.Add(new ValidationError(
                    "FuelTankCapacityLitres",
                    "Fuel-tank capacity must fit the fleet register and use no more than two decimal places."));
            }

            if (request.BatteryCapacityKwh.HasValue && request.BatteryCapacityKwh.Value <= 0)
            {
                errors.Add(new ValidationError(
                    "BatteryCapacityKwh",
                    "Battery capacity must be greater than zero."));
            }
            else if (request.BatteryCapacityKwh.HasValue &&
                (request.BatteryCapacityKwh.Value > MaximumEnergyCapacity ||
                 HasMoreThanDecimalPlaces(request.BatteryCapacityKwh.Value, 2)))
            {
                errors.Add(new ValidationError(
                    "BatteryCapacityKwh",
                    "Battery capacity must fit the fleet register and use no more than two decimal places."));
            }

            if (selectedPropulsion == null)
            {
                return;
            }

            string code = selectedPropulsion.Code ?? string.Empty;
            if (UsesFuel(code) && !request.FuelTankCapacityLitres.HasValue)
            {
                errors.Add(new ValidationError(
                    "FuelTankCapacityLitres",
                    "Fuel-tank capacity is required for this propulsion type."));
            }

            if (UsesBattery(code) && !request.BatteryCapacityKwh.HasValue)
            {
                errors.Add(new ValidationError(
                    "BatteryCapacityKwh",
                    "Battery capacity is required for this propulsion type."));
            }
        }

        private static LookupOption FindOption(IList<LookupOption> options, int? selectedId)
        {
            if (options == null || !selectedId.HasValue)
            {
                return null;
            }

            return options.FirstOrDefault(option => option != null && option.Id == selectedId.Value);
        }

        private static IList<string> GetExpiredDocuments(RegisterBusRequest request, DateTime today)
        {
            IList<string> expired = new List<string>();
            if (request.LicenceExpiryDate.Value.Date < today.Date)
            {
                expired.Add("licence");
            }

            if (request.RoadworthyExpiryDate.Value.Date < today.Date)
            {
                expired.Add("roadworthy certificate");
            }

            if (request.InsuranceExpiryDate.Value.Date < today.Date)
            {
                expired.Add("insurance");
            }

            return expired;
        }

        private static ServiceResult<BusRegistrationResult> DuplicateFailure(DuplicateBusField field)
        {
            switch (field)
            {
                case DuplicateBusField.FleetNumber:
                    return ServiceResult<BusRegistrationResult>.Failure(
                        "FleetNumber",
                        "A bus with this fleet number already exists.");
                case DuplicateBusField.RegistrationNumber:
                    return ServiceResult<BusRegistrationResult>.Failure(
                        "RegistrationNumber",
                        "A bus with this registration number already exists.");
                case DuplicateBusField.Vin:
                    return ServiceResult<BusRegistrationResult>.Failure(
                        "Vin",
                        "A bus with this VIN already exists.");
                default:
                    return ServiceResult<BusRegistrationResult>.Failure(
                        string.Empty,
                        "A bus with one or more of these identifiers already exists.");
            }
        }

        private static bool UsesFuel(string propulsionCode)
        {
            return string.Equals(propulsionCode, "Petrol", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propulsionCode, "Diesel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propulsionCode, "Hybrid", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesBattery(string propulsionCode)
        {
            return string.Equals(propulsionCode, "Electric", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propulsionCode, "Hybrid", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownPropulsion(string propulsionCode)
        {
            return string.Equals(propulsionCode, "Petrol", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propulsionCode, "Diesel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propulsionCode, "Hybrid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propulsionCode, "Electric", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefinedState(BusOperationalState? state)
        {
            return !state.HasValue || Enum.IsDefined(typeof(BusOperationalState), state.Value);
        }

        private static bool HasMoreThanDecimalPlaces(decimal value, int decimalPlaces)
        {
            return value != decimal.Round(value, decimalPlaces, MidpointRounding.ToEven);
        }

        private static string NormalizeIdentifier(string value)
        {
            string normalized = NormalizeText(value);
            return normalized == null ? null : normalized.ToUpperInvariant();
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
