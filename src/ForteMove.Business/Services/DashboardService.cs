using System;
using ForteMove.Business.Contracts;
using ForteMove.Business.Time;
using ForteMove.Models.Operations;

namespace ForteMove.Business.Services
{
    public sealed class DashboardService
    {
        private readonly IDashboardRepository repository;
        private readonly IClock clock;

        public DashboardService(IDashboardRepository repository)
            : this(repository, new SystemClock())
        {
        }

        public DashboardService(IDashboardRepository repository, IClock clock)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            if (clock == null)
            {
                throw new ArgumentNullException("clock");
            }

            this.repository = repository;
            this.clock = clock;
        }

        public DashboardSummary GetSummary()
        {
            return repository.GetSummary(clock.OperationalNow) ?? new DashboardSummary();
        }
    }
}
