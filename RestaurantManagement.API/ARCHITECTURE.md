# API architecture

- `Configuration/AppSettings.cs`: typed centralized configuration.
- `Infrastructure/SqlConnectionFactory.cs`: SQL Server connection creation.
- `Infrastructure/SqlDataAccess.cs`: asynchronous parameterized ADO.NET helpers.
- `Controllers/`: resource-oriented HTTP endpoints (GET/POST/PUT/DELETE).
- `Services/EmailService.cs`: SMTP password-reset delivery.
- `SysLogs/`: rolling runtime log files, ignored by Git.
- `Database/Scripts/*.txt`: manually executed SQL tables, indexes, views and procedures. Database object definitions stay out of C# source.
- Razorpay/live payment integration is postponed.

Serilog writes daily files with 10 MB size rolling and a 30-file retention cap. Request bodies and credentials are not logged. All SQL inputs must remain parameterized. Large-table performance requires selective filters, supporting indexes, bounded paging, query-plan review and production-like load tests.
