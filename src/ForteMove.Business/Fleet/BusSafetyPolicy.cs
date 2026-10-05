using System;
using System.Collections.Generic;
using System.Linq;
using ForteMove.Business.Maintenance;
using ForteMove.Models.Fleet;
using ForteMove.Models.Maintenance;
namespace ForteMove.Business.Fleet
{
    public static class BusSafetyPolicy
    {
        public static IList<string> Compliance(DateTime licence,DateTime roadworthy,DateTime insurance,DateTime requiredThrough)
        {
            var e=new List<string>();
            if(licence.Date<requiredThrough.Date)e.Add("Vehicle licence is expired or does not cover the required date.");
            if(roadworthy.Date<requiredThrough.Date)e.Add("Roadworthy certificate is expired or does not cover the required date.");
            if(insurance.Date<requiredThrough.Date)e.Add("Insurance is expired or does not cover the required date.");
            return e;
        }
        public static IList<string> MaintenanceBlocks(IEnumerable<MaintenancePlan> plans,DateTime today,decimal odometer)
        {
            return (plans??new MaintenancePlan[0]).Where(p=>p.IsActive && p.BlocksOperationWhenOverdue && MaintenancePolicy.Evaluate(p,today,odometer)==MaintenanceDueState.Overdue)
                .Select(p=>"Overdue service: "+p.PlanCode+" "+p.ServiceName+".").ToList();
        }
        public static IList<string> OperationalBlocks(BusSafetyContext c,DateTime today)
        {
            var e=new List<string>();
            if(c==null || c.Bus==null){e.Add("The bus is unavailable.");return e;}
            var b=c.Bus;
            if(b.BaseOperationalState==BusOperationalState.Retired)e.Add("A retired bus cannot return to service.");
            if(c.HasInProgressMaintenance)e.Add("Maintenance work is still in progress.");
            if(c.HasCriticalDefect)e.Add("An unresolved Critical defect prevents operation.");
            if(!b.GrossVehicleMassKg.HasValue || b.GrossVehicleMassKg<=0)e.Add("Approved gross vehicle mass is required.");
            if(!c.CategoryActive || b.PassengerCapacity<1 || b.PassengerCapacity>200)e.Add("An active category and valid passenger capacity are required.");
            e.AddRange(Compliance(b.LicenceExpiryDate,b.RoadworthyExpiryDate,b.InsuranceExpiryDate,today));
            e.AddRange(MaintenanceBlocks(c.Plans,today,b.OdometerKilometres));
            return e;
        }
    }
}
