# Restaurant Management API

.NET 10 ASP.NET Core Web API using ADO.NET (Microsoft.Data.SqlClient), Controller → Service → Repository, Swagger, ASP.NET Core Identity password hashing, JWT access tokens and rotating refresh tokens.

## Configure

Local development values are in appsettings.json. Override secrets and deployment-specific values with environment variables:
- AppSettings__Database__ConnectionString
- AppSettings__Jwt__SigningKey
- AppSettings__PublicBaseUrl
- AppSettings__AllowedOrigins__0
- AppSettings__Paths__Logs
- AppSettings__Paths__UserImages
- AppSettings__Paths__Temp

Never commit production secrets. Use HTTPS and a cryptographically random JWT signing key of at least 32 characters. The committed SQL connection string is a local-development example only.

## Database setup / upgrade

Run database/auth-schema.sql against the intended SQL Server database. It creates the auth schema/tables if missing and adds CountryCode to an existing auth.Users table. Review existing duplicate emails/mobile values before adding unique indexes. Existing mobile values should be normalized to E.164 before using the new mobile-login contract; do not blindly rewrite production identity data.

## Authentication contract

- POST /api/v1/auth/register: fullName, email, optional countryCode, optional mobileNumber, password. Country code and mobile are stored as normalized E.164 when supplied.
- POST /api/v1/auth/login: emailOrMobile and password. Mobile login uses the full international number (for example +919876543210).
- POST /api/v1/auth/refresh: rotates a refresh token; old token is revoked atomically.
- POST /api/v1/auth/logout: revokes the submitted refresh token.
- GET /api/v1/auth/me: requires Bearer access token.
- GET /api/v1/auth/owner-check: sample role-protected endpoint (Owner/Admin).

Responses use success, message, data, and stable errorCode fields. Duplicate email/mobile returns HTTP 409. Registration now assigns the least-privilege Staff role; it no longer silently grants Owner to every public registrant. Owner/Admin assignment must be an explicit trusted administrative workflow.

Passwords are stored using ASP.NET Core PasswordHasher<TUser>; refresh tokens are random opaque values and only SHA-256 hashes are persisted. Never log passwords or tokens. Email verification is configurable; set AppSettings__Auth__RequireEmailVerification=true only when the verification workflow is deployed end-to-end.

## Run

Create/configure the SQL Server database, apply the schema, configure connection string and JWT signing key, then:

```bash
dotnet restore RestaurantManagement.API/RestaurantManagement.API.csproj
dotnet build RestaurantManagement.API/RestaurantManagement.API.csproj --configuration Release
dotnet run --project RestaurantManagement.API
```

Swagger is at /swagger; health is at /health. Run only over HTTPS outside local development.

## Verification status

Repository edits are committed, but this environment has not executed .NET restore/build, SQL migrations, endpoint integration tests, or a deployment. GitHub Actions is configured to build the API on DevApi pushes and pull requests. Production readiness still requires automated tests, rate limiting, operational secret management, and review of the existing database before migration.
