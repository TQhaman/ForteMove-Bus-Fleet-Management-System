using System;
using System.Collections.Generic;
using ForteMove.Models.Passengers;

namespace ForteMove.Business.Contracts
{
    public interface IPassengerRepository
    {
        PassengerRegistrationResult RegisterPassenger(PassengerRegistrationAggregate aggregate, DateTime utcNow);
        PassengerAccountDetails GetAccount(long userAccountId);
        PassengerWalletDetails GetWallet(long userAccountId, int recentTransactionCount);
        IList<JourneyRouteOption> GetJourneyRoutes();
        IList<PassengerJourneyCandidate> FindJourneyCandidates(JourneyQuery query);
        PassengerJourneyCandidate GetJourneyCandidate(long tripId);
        TopUpResult GetCompletedTopUp(long userAccountId, Guid operationToken);
        TopUpResult TopUpWallet(long userAccountId, TopUpRequest request, DateTime utcNow);
        TicketPurchaseResult GetCompletedPurchase(long userAccountId, Guid operationToken);
        TicketPurchaseResult PurchaseTicket(long userAccountId, PurchaseTicketRequest request, DateTime operationalNow, DateTime utcNow);
        IList<TicketListItem> GetTickets(long userAccountId, TicketListMode mode, DateTime operationalNow);
        TicketDetails GetTicketDetails(long userAccountId, long ticketId);
        PassengerHomeDetails GetHome(long userAccountId, DateTime operationalNow);
    }
}
