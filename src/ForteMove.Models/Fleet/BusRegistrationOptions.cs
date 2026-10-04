using System.Collections.Generic;

namespace ForteMove.Models.Fleet
{
    public sealed class BusRegistrationOptions
    {
        public BusRegistrationOptions()
        {
            Categories = new List<LookupOption>();
            PropulsionTypes = new List<LookupOption>();
        }

        public IList<LookupOption> Categories { get; set; }

        public IList<LookupOption> PropulsionTypes { get; set; }

        public string SuggestedFleetNumber { get; set; }
    }
}
