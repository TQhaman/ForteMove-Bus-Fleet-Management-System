using System;
using System.Configuration;
using ForteMove.Business.Services;
using ForteMove.Data.Repositories;

namespace ForteMove.Web.Infrastructure
{
    public static class ServiceFactory
    {
        private const string ConnectionStringName = "ForteMove";

        public static AuthenticationService CreateAuthenticationService()
        {
            return new AuthenticationService(
                new SqlAuthenticationRepository(GetConnectionString()));
        }

        public static BusService CreateBusService()
        {
            return new BusService(
                new SqlBusRepository(GetConnectionString()));
        }

        public static RouteService CreateRouteService()
        {
            return new RouteService(
                new SqlRouteRepository(GetConnectionString()));
        }

        public static DashboardService CreateDashboardService()
        {
            return new DashboardService(
                new SqlDashboardRepository(GetConnectionString()));
        }

        public static SchedulingService CreateSchedulingService()
        {
            return new SchedulingService(
                new SqlSchedulingRepository(GetConnectionString()));
        }

        private static string GetConnectionString()
        {
            ConnectionStringSettings settings =
                ConfigurationManager.ConnectionStrings[ConnectionStringName];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException(
                    "The ForteMove database connection string is not configured.");
            }

            return settings.ConnectionString;
        }
    }
}
