using System.Collections.Generic;
using ForteMove.Models.Fleet;

namespace ForteMove.Business.Contracts
{
    public interface IBusRepository
    {
        IList<LookupOption> GetActiveBusCategories();

        IList<LookupOption> GetActivePropulsionTypes();

        int GetNextFleetNumberSequence();

        long RegisterBus(Bus bus, long actorUserAccountId);

        IList<BusListItem> GetFleetList(FleetQuery query);
    }
}
