using System.Collections.Generic;
using ForteMove.Models.Maintenance;
namespace ForteMove.Models.Fleet
{
    public sealed class BusSafetyContext
    {
        public BusSafetyContext() { Plans = new List<MaintenancePlan>(); }
        public BusDetails Bus { get; set; }
        public bool CategoryActive { get; set; }
        public bool HasCriticalDefect { get; set; }
        public bool HasInProgressMaintenance { get; set; }
        public bool HasActiveExecution { get; set; }
        public IList<MaintenancePlan> Plans { get; set; }
    }
}
