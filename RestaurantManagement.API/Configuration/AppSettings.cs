using RestaurantManagement.API.Common.Constants;

namespace RestaurantManagement.API.Configuration;

public sealed class AppSettings
{
    public const string SectionName = "App";
    public string Name { get; set; } = "swapDigit API";
    public DatabaseSettings Database { get; set; } = new();
    public JwtSettings Jwt { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
    public FileStorageSettings FileStorage { get; set; } = new();
    public BusinessSettings Business { get; set; } = new();
}
public sealed class DatabaseSettings { public string ConnectionString { get; set; } = ""; public int CommandTimeoutSeconds { get; set; } = 30; }
public sealed class JwtSettings { public string Issuer { get; set; } = "swapDigit"; public string Audience { get; set; } = "swapDigit.Clients"; public string SigningKey { get; set; } = ""; public int AccessTokenMinutes { get; set; } = 60; public int RefreshTokenDays { get; set; } = 14; }
public sealed class EmailSettings { public string SmtpHost { get; set; } = ""; public int SmtpPort { get; set; } = 587; public bool EnableSsl { get; set; } = true; public string UserName { get; set; } = ""; public string Password { get; set; } = ""; public string FromEmail { get; set; } = ""; public string FromName { get; set; } = "swapDigit"; }
public sealed class CorsSettings { public string[] AllowedOrigins { get; set; } = []; }
public sealed class FileStorageSettings { public string RootPath { get; set; } = "App_Data/Uploads"; public long MaxFileSizeMb { get; set; } = 10; }
public sealed class BusinessSettings { public string CurrencyCode { get; set; } = "INR"; public decimal DefaultTaxPercent { get; set; } = 5m; public int PasswordMinimumLength { get; set; } = AuthConstants.MinimumPasswordLength; }
