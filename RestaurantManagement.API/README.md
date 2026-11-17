# swapDigit API

ASP.NET Core Web API (.NET 10) using **ADO.NET / Microsoft.Data.SqlClient**. No Entity Framework and no Dapper.

## Configuration
All settings are centralized under the `App` section in appsettings.json and bound to typed classes in Configuration/AppSettings.cs. Connection strings, JWT signing keys, SMTP credentials and sender email must be supplied with .NET User Secrets locally or environment variables in deployment. Do not commit real credentials.

Example local User Secrets (run from RestaurantManagement.API):
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

Environment variable equivalents use double underscores, e.g. `App__Database__ConnectionString`, `App__Jwt__SigningKey`, `App__Email__SmtpHost`, `App__Email__Password`.

## Database
Execute `database/SwapDigit_Database_Setup.txt` in SQL Server Management Studio. It creates the database and tables used by the API. Change the database name there only if you also update the connection string.

## Run
```bash
dotnet restore
dotnet run --project RestaurantManagement.API
```
Swagger: `/swagger`; health: `/health`.

## API modules
Auth, staff/users, products/categories, floors/tables, orders/order items, reservations, KOT, bills, inventory/stock movements, dashboard and sales reports, restaurant settings, subscription plans/current subscription, and support tickets.

## Important
- Public owner registration is allowed only while no Owner exists.
- Staff accounts are created and managed by an authenticated Owner/Manager.
- Cash/UPI/Card are stored as payment methods on orders/bills. A live payment gateway is not enabled by this API baseline.
- Email-based password reset requires valid SMTP credentials.
- API must be served over HTTPS outside local development.
