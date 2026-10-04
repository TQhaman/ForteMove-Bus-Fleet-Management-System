using System;
using ForteMove.Models.Assignments;

namespace ForteMove.Business.Contracts
{
    public interface IAssignmentRepository
    {
        AssignmentData GetAssignmentData(DateTime serviceDate);
        int GetRouteFamiliarity(long driverProfileId, long routeId);
        AssignmentDetails GetDetails(long tripId);
        void ConfirmAssignments(ConfirmAssignmentsRequest request, long actorUserAccountId, DateTime operationalNow, DateTime utcNow);
        void ChangeAssignment(ConfirmAssignmentItem replacement, byte[] currentAssignmentRowVersion, long actorUserAccountId, DateTime operationalNow, DateTime utcNow);
        void RemoveAssignment(AssignmentMutationRequest request, long actorUserAccountId, DateTime utcNow);
        void ResolveReview(long tripId, byte[] tripRowVersion, string note, long actorUserAccountId, DateTime utcNow);
    }
}
