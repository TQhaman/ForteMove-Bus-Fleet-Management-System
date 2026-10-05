# Slice 9 — Changed-file inventory

Branch: **Slice-9**. Base/HEAD: **c65a0b6d0e435c0fa3a30e095f433c389b3c0129**. No commit or push.

48 tracked files modified; 62 new files; no tracked files deleted. Build outputs and verification copies are ignored, not source changes.

## Implementation map

- Database: only 0009; five maintenance/history tables and same-Bus relationships.
- Models: typed maintenance requests/results and focused safety context extensions.
- Business: maintenance/provider services, pure safety/due policies, repository contracts and eligibility integration.
- Data: serializable maintenance/provider repositories, transaction-aware defect/fuel/status helpers and existing repository integration.
- Web: ten administrator Maintenance pages, composition/navigation/dashboard/context links and Driver departure feedback. No new Driver or provider-login pages.
- Verification: deterministic Business, rollback, disposable concurrency, runtime/precompile and integrity runners.
- Documentation: README/database README, verification, manual acceptance and this inventory.

## Modified files

~~~text
README.md
database/README.md
src/ForteMove.Business/ForteMove.Business.csproj
src/ForteMove.Business/Fuel/FuelPolicy.cs
src/ForteMove.Business/Identifiers/IdentifierCodePolicy.cs
src/ForteMove.Business/Services/AssignmentService.cs
src/ForteMove.Business/Services/BusService.cs
src/ForteMove.Business/Services/DriverOperationsService.cs
src/ForteMove.Business/Services/PassengerService.cs
src/ForteMove.Data/ForteMove.Data.csproj
src/ForteMove.Data/Internal/SqlFuelLifecycle.cs
src/ForteMove.Data/Repositories/SqlAssignmentRepository.cs
src/ForteMove.Data/Repositories/SqlBusRepository.cs
src/ForteMove.Data/Repositories/SqlDashboardRepository.cs
src/ForteMove.Data/Repositories/SqlPassengerRepository.cs
src/ForteMove.Data/Repositories/SqlTripOperationsRepository.cs
src/ForteMove.Models/Assignments/AssignmentModels.cs
src/ForteMove.Models/Fleet/BusDetails.cs
src/ForteMove.Models/ForteMove.Models.csproj
src/ForteMove.Models/Operations/DashboardSummary.cs
src/ForteMove.Models/Operations/DriverOperationsModels.cs
src/ForteMove.Models/Passengers/PassengerModels.cs
src/ForteMove.Web/Admin/Admin.Master
src/ForteMove.Web/Admin/Admin.Master.cs
src/ForteMove.Web/Admin/Admin.Master.designer.cs
src/ForteMove.Web/Admin/Assignments/AssignmentDetails.aspx
src/ForteMove.Web/Admin/Assignments/AssignmentDetails.aspx.cs
src/ForteMove.Web/Admin/Assignments/AssignmentDetails.aspx.designer.cs
src/ForteMove.Web/Admin/Dashboard.aspx
src/ForteMove.Web/Admin/Dashboard.aspx.cs
src/ForteMove.Web/Admin/Dashboard.aspx.designer.cs
src/ForteMove.Web/Admin/EditBus.aspx
src/ForteMove.Web/Admin/EditBus.aspx.cs
src/ForteMove.Web/Admin/EditBus.aspx.designer.cs
src/ForteMove.Web/Admin/FleetDetails.aspx
src/ForteMove.Web/Admin/FleetDetails.aspx.cs
src/ForteMove.Web/Admin/FleetDetails.aspx.designer.cs
src/ForteMove.Web/Admin/Operations/DefectDetails.aspx
src/ForteMove.Web/Admin/Operations/DefectDetails.aspx.cs
src/ForteMove.Web/Admin/Operations/DefectDetails.aspx.designer.cs
src/ForteMove.Web/Admin/Operations/ExceptionDetails.aspx
src/ForteMove.Web/Admin/Operations/ExceptionDetails.aspx.cs
src/ForteMove.Web/Admin/Operations/ExceptionDetails.aspx.designer.cs
src/ForteMove.Web/Content/fortemove.css
src/ForteMove.Web/Driver/PreTripCheck.aspx.cs
src/ForteMove.Web/Driver/TripDetails.aspx.cs
src/ForteMove.Web/ForteMove.Web.csproj
src/ForteMove.Web/Infrastructure/ServiceFactory.cs
~~~

## New files

~~~text
SLICE_9_CHANGED_FILES.md
SLICE_9_MANUAL_TESTS.md
SLICE_9_VERIFICATION.md
database/migrations/0009_MaintenanceManagement.sql
src/ForteMove.Business/Contracts/IMaintenanceRepository.cs
src/ForteMove.Business/Contracts/IRepairProviderRepository.cs
src/ForteMove.Business/Exceptions/MaintenancePersistenceException.cs
src/ForteMove.Business/Fleet/BusSafetyPolicy.cs
src/ForteMove.Business/Maintenance/MaintenancePolicy.cs
src/ForteMove.Business/Services/MaintenanceService.cs
src/ForteMove.Business/Services/RepairProviderService.cs
src/ForteMove.Data/Internal/SqlBusStatusHistory.cs
src/ForteMove.Data/Internal/SqlDefectLifecycle.cs
src/ForteMove.Data/Internal/SqlMaintenanceLifecycle.cs
src/ForteMove.Data/Repositories/SqlMaintenanceRepository.Plans.cs
src/ForteMove.Data/Repositories/SqlMaintenanceRepository.Queries.cs
src/ForteMove.Data/Repositories/SqlMaintenanceRepository.WorkOrders.cs
src/ForteMove.Data/Repositories/SqlMaintenanceRepository.cs
src/ForteMove.Data/Repositories/SqlRepairProviderRepository.cs
src/ForteMove.Models/Fleet/BusSafetyModels.cs
src/ForteMove.Models/Maintenance/MaintenanceEnums.cs
src/ForteMove.Models/Maintenance/MaintenanceModels.cs
src/ForteMove.Models/Maintenance/MaintenanceRequests.cs
src/ForteMove.Web/Admin/Maintenance/CompleteWorkOrder.aspx
src/ForteMove.Web/Admin/Maintenance/CompleteWorkOrder.aspx.cs
src/ForteMove.Web/Admin/Maintenance/CompleteWorkOrder.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/CreateWorkOrder.aspx
src/ForteMove.Web/Admin/Maintenance/CreateWorkOrder.aspx.cs
src/ForteMove.Web/Admin/Maintenance/CreateWorkOrder.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/Due.aspx
src/ForteMove.Web/Admin/Maintenance/Due.aspx.cs
src/ForteMove.Web/Admin/Maintenance/Due.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/Overview.aspx
src/ForteMove.Web/Admin/Maintenance/Overview.aspx.cs
src/ForteMove.Web/Admin/Maintenance/Overview.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/Plans.aspx
src/ForteMove.Web/Admin/Maintenance/Plans.aspx.cs
src/ForteMove.Web/Admin/Maintenance/Plans.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/RepairProviders.aspx
src/ForteMove.Web/Admin/Maintenance/RepairProviders.aspx.cs
src/ForteMove.Web/Admin/Maintenance/RepairProviders.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/ReturnToService.aspx
src/ForteMove.Web/Admin/Maintenance/ReturnToService.aspx.cs
src/ForteMove.Web/Admin/Maintenance/ReturnToService.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/ServiceHistory.aspx
src/ForteMove.Web/Admin/Maintenance/ServiceHistory.aspx.cs
src/ForteMove.Web/Admin/Maintenance/ServiceHistory.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/WorkOrderDetails.aspx
src/ForteMove.Web/Admin/Maintenance/WorkOrderDetails.aspx.cs
src/ForteMove.Web/Admin/Maintenance/WorkOrderDetails.aspx.designer.cs
src/ForteMove.Web/Admin/Maintenance/WorkOrders.aspx
src/ForteMove.Web/Admin/Maintenance/WorkOrders.aspx.cs
src/ForteMove.Web/Admin/Maintenance/WorkOrders.aspx.designer.cs
src/ForteMove.Web/Infrastructure/MaintenanceAdminPage.cs
src/ForteMove.Web/Infrastructure/MaintenancePresentation.cs
tools/Verification/Invoke-Slice9IntegrityVerification.ps1
tools/Verification/Invoke-Slice9PageRenderVerification.ps1
tools/Verification/Invoke-Slice9Verification.ps1
tools/Verification/Slice9BusinessVerification.cs
tools/Verification/Slice9ConcurrencyVerification.cs
tools/Verification/Slice9PageRenderVerification.cs
tools/Verification/Slice9RollbackVerification.cs
~~~

Migrations 0000–0008 and Content/Brand assets are unchanged. No packages or solution restructuring were introduced. New classic-project entries are included and verified.
