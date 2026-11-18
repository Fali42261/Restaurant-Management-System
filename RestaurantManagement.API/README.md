# swapDigit API

ASP.NET Core Web API (.NET 10), using **ADO.NET / Microsoft.Data.SqlClient** only (no EF Core or Dapper).

## Database scripts
All database object definitions are plain-text SQL under `Database/Scripts`. Run these files manually in SQL Server Management Studio (SSMS), in order:
1. `01_Tables_And_Seed_Data.txt` — database, tables, constraints and starter data.
2. `02_Performance_Indexes_Views_Procedures.txt` — indexes, read views and bounded-page stored procedures.
3. `03_Multi_Restaurant_Tenancy.txt` — owner/restaurant membership, per-restaurant data keys, subscription scoping and SQL Server row-level security. Existing records are assigned to a Legacy Restaurant; review this mapping before production.

The API does not auto-run SQL scripts. Configure the connection string after running the scripts.

## Local configuration
Run from RestaurantManagement.API:
```bash
dotnet user-secrets init
dotnet user-secrets set "App:Database:ConnectionString" "Server=localhost;Database=swapDigitDb;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "App:Jwt:SigningKey" "REPLACE_WITH_A_RANDOM_SECRET_AT_LEAST_32_CHARACTERS"
dotnet user-secrets set "App:Email:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "App:Email:SmtpPort" "587"
dotnet user-secrets set "App:Email:EnableSsl" "true"
dotnet user-secrets set "App:Email:UserName" "your-email@gmail.com"
dotnet user-secrets set "App:Email:Password" "your-google-app-password"
dotnet user-secrets set "App:Email:FromEmail" "your-email@gmail.com"
```
Use a Google App Password for Gmail. Hosted environment variables use double underscores, e.g. `App__Database__ConnectionString`. Never commit secrets.

## Logs
Runtime logs are written to `SysLogs/swapDigit-yyyyMMdd.log`. Files roll at 10 MB and at most 30 files are retained. HTTP method/path/status/duration and trace ID are recorded. Request/response bodies, passwords, JWTs and query-string values are not logged. The folder is created automatically; runtime logs are excluded from Git.

## Run
```bash
dotnet restore
dotnet run --project RestaurantManagement.API
```
Swagger: `/swagger`; health: `/health`.

## Multi-restaurant flow
- Owner registration creates the Owner account and its first restaurant.
- Login returns the Owner's/Staff member's assigned restaurant list.
- Call `GET /api/v1/restaurants`, then `POST /api/v1/restaurants/{restaurantId}/select` to receive a one-hour access token and refresh token scoped to that restaurant.
- Owners can create additional restaurants with `POST /api/v1/restaurants` and rename them with `PUT /api/v1/restaurants/{restaurantId}`.
- Every restaurant has its own subscription record. Subscription requests are saved as `PendingPayment`; no payment gateway or automatic activation is implemented.
- SQL Server row-level security uses the selected restaurant claim for business tables. Staff membership and roles are restaurant-specific.

## API conventions
GET reads, POST creates/actions, PUT updates, DELETE archives/removes. Payment method values may be stored against orders and bills, but **Razorpay and all live payment gateway integration are intentionally deferred**.

## Scale and testing
ADO.NET commands are parameterized and asynchronous. SQL indexes and bounded-page stored procedures are included as database scripts. The 100-million-row goal must be validated with realistic data, execution plans, and load tests on the target SQL Server hardware; no throughput guarantee is made without that verification.

## Delivery sequence
Run SQL scripts → configure secrets → test API independently in Swagger → connect DevUI only after API/database acceptance → final integrated UI/API testing.
