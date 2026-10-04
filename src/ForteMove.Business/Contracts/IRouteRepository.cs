using System.Collections.Generic;
using ForteMove.Models.Routing;

namespace ForteMove.Business.Contracts
{
    public interface IRouteRepository
    {
        int GetNextRouteCodeSequence();

        int GetNextStopCodeSequence();

        IList<StopOption> GetActiveStops();

        RouteCreationResult CreateRoute(
            RouteCreationAggregate aggregate,
            long actorUserAccountId);

        IList<RouteListItem> GetRouteList(RouteQuery query);

        RouteDetails GetRouteDetails(long routeId);
    }
}
