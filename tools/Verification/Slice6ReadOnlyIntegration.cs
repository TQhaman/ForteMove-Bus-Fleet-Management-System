using System;
using ForteMove.Data.Repositories;
using ForteMove.Models.Passengers;

internal static class Slice6ReadOnlyIntegration
{
    public static int Main()
    {
        try
        {
            const string connection = @"Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True;Application Name=ForteMove.Slice6Verification";
            SqlPassengerRepository passengers = new SqlPassengerRepository(connection);
            int routes = passengers.GetJourneyRoutes().Count;
            int journeys = passengers.FindJourneyCandidates(new JourneyQuery { ServiceDate = new DateTime(2026,10,6) }).Count;

            SqlAssignmentRepository assignments = new SqlAssignmentRepository(connection);
            var assignmentData = assignments.GetAssignmentData(new DateTime(2026,10,6));
            var details = assignments.GetDetails(48);
            if (details == null || details.Trip == null) throw new InvalidOperationException("Assignment details mapping failed.");

            SqlSchedulingRepository schedules = new SqlSchedulingRepository(connection);
            var changeOptions = schedules.GetChangeOptions(2, new DateTime(2026,10,6));
            if (changeOptions == null) throw new InvalidOperationException("Schedule change dependency mapping failed.");

            SqlAuthenticationRepository authentication = new SqlAuthenticationRepository(connection);
            var principal = authentication.GetPrincipalContext(1);
            if (principal == null) throw new InvalidOperationException("Existing administrator principal mapping failed.");

            Console.WriteLine("Slice 6 read-only integration passed: routes={0}, candidates={1}, assignmentTrips={2}, purchasedTickets={3}, changeTrips={4}.", routes, journeys, assignmentData.Trips.Count, details.PurchasedTicketCount, changeOptions.ExistingTripsFromCutover.Count);
            return 0;
        }
        catch(Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }
}
