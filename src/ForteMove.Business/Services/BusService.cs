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

            if (!request.GrossVehicleMassKg.HasValue || request.GrossVehicleMassKg.Value <= 0)
            {
                errors.Add(new ValidationError(
                    "GrossVehicleMassKg",
                    "Gross vehicle mass must be greater than zero."));
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

            BusOperationalState baseState = request.BaseOperationalState ?? BusOperationalState.Operational;
            if (!Enum.IsDefined(typeof(BusOperationalState), baseState))
            {
                errors.Add(new ValidationError("BaseOperationalState", "Select a valid vehicle status."));
            }

            if (request.LicenceExpiryDate.HasValue && request.RoadworthyExpiryDate.HasValue && request.InsuranceExpiryDate.HasValue &&
                baseState == BusOperationalState.Operational)
            {
                AddOperationalComplianceErrors(errors, request.LicenceExpiryDate.Value,
                    request.RoadworthyExpiryDate.Value, request.InsuranceExpiryDate.Value, clock.Today);
            }

            if (actorUserAccountId <= 0)
            {
                errors.Add(new ValidationError(string.Empty, "A valid administrator is required to register a bus."));
            }

            if (errors.Count > 0)
            {
                return ServiceResult<BusRegistrationResult>.Failure(errors);
            }

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
                GrossVehicleMassKg = request.GrossVehicleMassKg.Value,
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

                return ServiceResult<BusRegistrationResult>.Success(result);
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

        public BusDetails GetBusDetails(long busId)
        {
            BusDetails bus = busId <= 0 ? null : repository.GetBusDetails(busId);
            if (bus != null)
            {
                bus.RequiredLicenceCode = GetRequiredLicenceCode(bus.GrossVehicleMassKg);
            }
            return bus;
        }

        public ServiceResult<bool> UpdateBusEligibility(UpdateBusEligibilityRequest request, long actorUserAccountId)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null || request.BusId <= 0)
                return ServiceResult<bool>.Failure(string.Empty, "A valid bus is required.");
            BusRegistrationOptions options = GetRegistrationOptions();
            if (FindOption(options.Categories, request.BusCategoryId) == null)
                errors.Add(new ValidationError("BusCategoryId", "Select an active bus category."));
            if (!request.PassengerCapacity.HasValue || request.PassengerCapacity.Value < 1 || request.PassengerCapacity.Value > 200)
                errors.Add(new ValidationError("PassengerCapacity", "Passenger capacity must be between 1 and 200."));
            if (!request.GrossVehicleMassKg.HasValue || request.GrossVehicleMassKg.Value <= 0)
                errors.Add(new ValidationError("GrossVehicleMassKg", "Gross vehicle mass must be greater than zero."));
            ValidateRequiredDate(errors,"LicenceExpiryDate","Licence expiry date",request.LicenceExpiryDate);
            ValidateRequiredDate(errors,"RoadworthyExpiryDate","Roadworthy expiry date",request.RoadworthyExpiryDate);
            ValidateRequiredDate(errors,"InsuranceExpiryDate","Insurance expiry date",request.InsuranceExpiryDate);
            if (!request.BaseOperationalState.HasValue || !Enum.IsDefined(typeof(BusOperationalState),request.BaseOperationalState.Value))
                errors.Add(new ValidationError("BaseOperationalState","Select a valid vehicle status."));
            if (request.RowVersion == null || request.RowVersion.Length == 0)
                errors.Add(new ValidationError(string.Empty,"The bus record must be refreshed before saving."));
            if (request.BaseOperationalState == BusOperationalState.Operational && request.LicenceExpiryDate.HasValue && request.RoadworthyExpiryDate.HasValue && request.InsuranceExpiryDate.HasValue)
                AddOperationalComplianceErrors(errors,request.LicenceExpiryDate.Value,request.RoadworthyExpiryDate.Value,request.InsuranceExpiryDate.Value,clock.Today);
            if (errors.Count > 0) return ServiceResult<bool>.Failure(errors);
            BusDetails existing=repository.GetBusDetails(request.BusId);
            if(existing==null)return ServiceResult<bool>.Failure(string.Empty,"The bus no longer exists.");
            if(existing.BaseOperationalState==BusOperationalState.Retired && request.BaseOperationalState!=BusOperationalState.Retired)return ServiceResult<bool>.Failure("BaseOperationalState","A retired bus cannot be reactivated.");
            if(request.BaseOperationalState==BusOperationalState.Operational && existing.BaseOperationalState!=BusOperationalState.Operational)return ServiceResult<bool>.Failure("BaseOperationalState","Use Return to Service to restore this bus to Operational.");
            if(existing.Safety!=null&&existing.Safety.HasInProgressMaintenance&&request.BaseOperationalState!=BusOperationalState.UnderMaintenance)return ServiceResult<bool>.Failure("BaseOperationalState","Complete or cancel the work order before changing vehicle status.");
            if(existing.Safety!=null)existing.Safety.CategoryActive=true;
            existing.BusCategoryId=request.BusCategoryId.Value;existing.PassengerCapacity=request.PassengerCapacity.Value;
            existing.GrossVehicleMassKg=request.GrossVehicleMassKg;existing.LicenceExpiryDate=request.LicenceExpiryDate.Value.Date;
            existing.RoadworthyExpiryDate=request.RoadworthyExpiryDate.Value.Date;existing.InsuranceExpiryDate=request.InsuranceExpiryDate.Value.Date;
            existing.BaseOperationalState=request.BaseOperationalState.Value;existing.RowVersion=request.RowVersion;
            if(existing.BaseOperationalState==BusOperationalState.Operational && existing.Safety!=null)
            {
                existing.Safety.Bus=existing;var blocks=ForteMove.Business.Fleet.BusSafetyPolicy.OperationalBlocks(existing.Safety,clock.Today);
                if(blocks.Count>0)return ServiceResult<bool>.Failure("BaseOperationalState",string.Join(" ",blocks));
            }
            try{repository.UpdateBusEligibility(existing,actorUserAccountId);return ServiceResult<bool>.Success(true);}
            catch(InvalidOperationException ex){return ServiceResult<bool>.Failure(string.Empty,ex.Message);}
            catch(MaintenancePersistenceException ex){return ServiceResult<bool>.Failure(string.Empty,ex.Message);}
        }

        public static string GetRequiredLicenceCode(int? grossVehicleMassKg)
        {
            if (!grossVehicleMassKg.HasValue) return null;
            if (grossVehicleMassKg.Value <= 3500) return "B";
            return grossVehicleMassKg.Value <= 16000 ? "C1" : "C";
        }

        private static void AddOperationalComplianceErrors(IList<ValidationError> errors, DateTime licence, DateTime roadworthy, DateTime insurance, DateTime today)
        {
            foreach(var message in ForteMove.Business.Fleet.BusSafetyPolicy.Compliance(licence,roadworthy,insurance,today))
            {
                var field=message.StartsWith("Vehicle licence",StringComparison.Ordinal)?"LicenceExpiryDate":message.StartsWith("Roadworthy",StringComparison.Ordinal)?"RoadworthyExpiryDate":"InsuranceExpiryDate";
                errors.Add(new ValidationError(field,message+" Operational status is not permitted."));
            }
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
