using ForteMove.Business.Exceptions;
using ForteMove.Business.Routing;
using ForteMove.Models.Common;
using ForteMove.Models.Routing;

namespace ForteMove.Business.Services
{
    public sealed partial class RouteService
    {
        public StopCoordinateDetails GetStopCoordinateDetails(long stopId,long actorUserAccountId)
        {
            return stopId<=0||actorUserAccountId<=0?null:repository.GetStopCoordinates(stopId,actorUserAccountId);
        }
        public ServiceResult<StopCoordinateSaveResult> UpdateStopCoordinates(UpdateStopCoordinatesRequest request,long actorUserAccountId)
        {
            if(request==null||request.StopId<=0||actorUserAccountId<=0)return ServiceResult<StopCoordinateSaveResult>.Failure("","A valid Stop is required.");
            var errors=CoordinatePolicy.Validate(request.Latitude,request.Longitude);
            if(request.RowVersion==null||request.RowVersion.Length!=8)errors.Add(new ValidationError("","Reload the Stop before saving."));
            if(errors.Count>0)return ServiceResult<StopCoordinateSaveResult>.Failure(errors);
            try { return ServiceResult<StopCoordinateSaveResult>.Success(repository.UpdateStopCoordinates(request,actorUserAccountId)); }
            catch(StopCoordinatePersistenceException ex) { return ServiceResult<StopCoordinateSaveResult>.Failure("",ex.Message); }
        }
    }
}
