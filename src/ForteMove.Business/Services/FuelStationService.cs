using System;
using System.Collections.Generic;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fuel;
namespace ForteMove.Business.Services
{
    public sealed class FuelStationService
    {
        private readonly IFuelStationRepository repository;
        private readonly IClock clock;
        public FuelStationService(IFuelStationRepository repository,IClock clock=null) { this.repository=repository;this.clock=clock??new SystemClock(); }
        public IList<FuelStation> GetStations(long actor) { return repository.GetStations(actor); }
        public FuelStation GetStation(long id,long actor) { return repository.GetStation(id,actor); }
        public ServiceResult<long> Save(SaveFuelStationRequest request,long actor)
        {
            var errors=new List<ValidationError>();
            if(request==null) return ServiceResult<long>.Failure("","Station details are required.");
            request.StationName=(request.StationName??"").Trim();request.AreaDescription=(request.AreaDescription??"").Trim();
            if(request.StationName.Length==0 || request.StationName.Length>150) errors.Add(new ValidationError("StationName","Enter a Station name within 150 characters."));
            if(request.AreaDescription.Length==0 || request.AreaDescription.Length>250) errors.Add(new ValidationError("AreaDescription","Enter an area or address description within 250 characters."));
            if(!request.SupportsDiesel && !request.SupportsElectricCharging) errors.Add(new ValidationError("Capabilities","Choose at least one supported supply type."));
            if(request.FuelStationId<0 || (request.FuelStationId>0 && (request.RowVersion==null || request.RowVersion.Length!=8)))
                errors.Add(new ValidationError("","Reload the Station before saving."));
            if(errors.Count>0) return ServiceResult<long>.Failure(errors);
            try { return ServiceResult<long>.Success(repository.Save(request,actor,clock.UtcNow)); }
            catch(FuelPersistenceException error) { return ServiceResult<long>.Failure("",error.Message); }
        }
    }
}
