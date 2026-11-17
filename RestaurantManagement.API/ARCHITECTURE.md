# API architecture

This API is deliberately separated by responsibility and uses ADO.NET only.

- `Configuration/AppSettings.cs`: strongly typed configuration sections (Database, Jwt, Email, Cors, FileStorage, Business).
- `Infrastructure/SqlConnectionFactory.cs`: creates SQL Server connections from the single configured connection string.
- `Infrastructure/SqlDataAccess.cs`: parameterized ADO.NET query, execute and scalar helpers. Controllers do not create ad-hoc connection strings.
- `Controllers/ApiControllerBase.cs`: shared response helpers and current authenticated user ID.
- `Controllers/AuthController.cs`: owner bootstrap, login, JWT/refresh tokens, password change/reset and logout.
- `Controllers/CatalogController.cs`: menu categories and products.
- `Controllers/RestaurantController.cs`: floors, tables, orders, POS checkout, bills, reservations and kitchen tickets.
- `Controllers/InventoryController.cs`: inventory items and stock movements.
- `Controllers/ReportsController.cs`: dashboard KPIs, sales summaries and top-selling products.
- `Controllers/SettingsController.cs`: restaurant settings and subscription read APIs.
- `Controllers/UsersController.cs`: staff account management.
- `Controllers/SupportController.cs`: support tickets.
- `Services/EmailService.cs`: SMTP delivery for password reset codes.
- `Models/Requests.cs`: request contracts shared by controllers.

Configuration is loaded once through ASP.NET Core configuration and bound to AppSettings. Add new settings to AppSettings.cs and appsettings.json; inject `IOptions<AppSettings>` into the class that needs them. Never place credentials inside controller, repository, or service source files.
