using System.Collections.Generic;
using ForteMove.Models.Maintenance;
namespace ForteMove.Business.Contracts
{
    public interface IRepairProviderRepository
    {
        IList<RepairProvider> GetProviders();
        RepairProvider GetProvider(long id);
        long SaveProvider(SaveRepairProviderRequest request,long actor);
    }
}
