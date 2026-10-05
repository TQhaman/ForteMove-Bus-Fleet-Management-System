# Slice 6 acceptance harnesses

These optional source files preserve the checks used during Slice 6 acceptance. They are outside the solution and are not run by the database initializer or ordinary builds.

- `Slice6BusinessVerification.cs` uses a fake repository and fixed clock. Its registration inputs are synthetic in-memory test fixtures, not login credentials or a default account. It checks balances, eligibility, idempotent operations and password-hashing output.
- `Slice6ReadOnlyIntegration.cs` reads a Windows-authenticated local SQL database. It assumes particular Trip, Schedule and account IDs and fixed service dates from the original acceptance dataset; a fresh clone's database will not contain those records.
- `Slice6DatabaseRollback.sql` checks database constraints with synthetic records inside a transaction that is rolled back. It requires an existing Trip.
- `Slice6RefundRollbackIntegration.cs` exercises refund persistence with synthetic transactional records and rolls them back. It requires existing Trip/administrator records and uses Windows authentication.

Review and adapt local instance names, record IDs and dates before use. Use a disposable test database. Rollback can still consume SQL identity values; these harnesses are not a way to populate demonstration data. The checkpoint task did not run these database harnesses against the accepted local database.

Compile C# harnesses separately with the .NET Framework compiler and the required built ForteMove assemblies; keep executables and copied assemblies outside Git. No real account passwords, exported local database, or personal data are needed to share these sources.
