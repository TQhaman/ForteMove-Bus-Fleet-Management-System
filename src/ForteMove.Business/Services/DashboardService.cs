using System;
using ForteMove.Business.Contracts;
using ForteMove.Models.Operations;

namespace ForteMove.Business.Services
{
    public sealed class DashboardService
    {
        private readonly IDashboardRepository repository;

        public DashboardService(IDashboardRepository repository)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            this.repository = repository;
        }

        public DashboardSummary GetSummary()
        {
            return repository.GetSummary() ?? new DashboardSummary();
        }
    }
}
