using ForteMove.Models.Operations;

namespace ForteMove.Business.Contracts
{
    public interface IDashboardRepository
    {
        DashboardSummary GetSummary();
    }
}
