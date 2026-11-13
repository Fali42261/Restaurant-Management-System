# swapDigit — Restaurant Management

This repository contains the .NET MAUI restaurant-management client.

## Central configuration

See [RestaurantManagement.UI/Configuration/AppConfig.cs](RestaurantManagement.UI/Configuration/AppConfig.cs) for the shared application configuration registry, API routes, backend controller/action naming references, and payment-provider setting names.

Read [RestaurantManagement.UI/CONFIGURATION.md](RestaurantManagement.UI/CONFIGURATION.md) before adding API or payment integrations. Never commit real secret keys. Payment secret and webhook keys belong in backend deployment secrets; the client may contain only publishable keys.
