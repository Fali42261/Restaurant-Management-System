# Project configuration

The single central configuration registry for the MAUI client is:

- `RestaurantManagement.UI/Configuration/AppConfig.cs`

## What belongs there

- App-wide settings and defaults
- API base URL and API route constants
- Expected backend controller and method/action names
- Payment provider name, environment, and client-safe publishable key slot
- Names of deployment secrets/environment variables

When adding a new API integration, first add its route/controller/action identifiers to `AppConfig.cs`, then reference those constants from services. Avoid scattering literal route names, controller names, method names, or provider identifiers across Views and ViewModels.

## Security rule for API and payment keys

Do **not** commit real API secrets, payment secret keys, webhook signing secrets, database passwords, or private keys in `AppConfig.cs`, appsettings files, APKs, EXEs, or any client-side code. Anything shipped inside an Android/Windows client can be extracted.

- Client: only non-secret values such as an API URL and a payment provider's publishable/client key.
- Backend: keep payment secret keys and webhook secrets in the hosting provider's secret/environment-variable store.
- CI/CD: use GitHub Actions Secrets for build/deployment values; never paste secret values into commits or logs.

The `Backend.Controllers` and `Backend.Methods` entries in `AppConfig.cs` are a central naming convention/reference map. This repository currently contains the MAUI UI and service interfaces; it does not currently contain the backend controller implementations. Keep this map synchronized when the backend is added.

## Current integration status

The API base URL and payment provider are intentionally unconfigured. The route registry is a contract/reference for future service integration; adding constants does not itself create API endpoints or activate payment processing.
