using System;
using System.Collections.Generic;
using ForteMove.Models.Fuel;
namespace ForteMove.Business.Contracts
{
    public interface IFuelStationRepository
    {
        IList<FuelStation> GetStations(long actor);
        FuelStation GetStation(long id,long actor);
        long Save(SaveFuelStationRequest request,long actor,DateTime utc);
    }
}

