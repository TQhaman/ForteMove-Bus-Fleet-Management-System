using System.Collections.Generic;
using ForteMove.Models.Drivers;

namespace ForteMove.Business.Contracts
{
    public interface IDriverRepository
    {
        int GetNextEmployeeNumberSequence();
        long CreateDriver(DriverPersistenceRecord driver, long actorUserAccountId);
        IList<Driver> GetDrivers();
        Driver GetDriver(long driverProfileId);
        Driver GetDriverByUserAccount(long userAccountId);
        void UpdateDriver(UpdateDriverRequest request, long actorUserAccountId);
    }
}
