# Restaurant Management API
.NET 10 ASP.NET Core API using ADO.NET (Microsoft.Data.SqlClient), Repository Pattern, Swagger, JWT access tokens and rotating refresh tokens.

## Configure
Edit AppSettings in appsettings.json for local values. Production should override secrets using environment variables:
- AppSettings__Database__ConnectionString
- AppSettings__Jwt__SigningKey
- AppSettings__PublicBaseUrl
- AppSettings__AllowedOrigins__0
- AppSettings__Paths__Logs
- AppSettings__Paths__UserImages
- AppSettings__Paths__Temp
Never commit production secrets. Use HTTPS and a random JWT signing key (32+ chars).

## First run
Create the SQL Server database, execute database/auth-schema.sql, configure connection string and JWT key, then run dotnet restore and dotnet run --project RestaurantManagement.API. Swagger is at /swagger; health at /health.

## Endpoints
POST /api/v1/auth/register, login, refresh, logout; GET /api/v1/auth/me (Bearer); GET /api/v1/auth/owner-check (Owner/Admin).
Registration assigns Owner. Passwords use ASP.NET Core PasswordHasher; refresh tokens are stored as SHA-256 hashes. Add rate limiting and account verification before public production release.

Build is configured in GitHub Actions. The source has not yet been compiled in this authoring environment; verify CI and SQL integration before deployment.