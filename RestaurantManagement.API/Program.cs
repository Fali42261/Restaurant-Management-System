using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.SectionName));
var appSettings = builder.Configuration.GetSection(AppSettings.SectionName).Get<AppSettings>() ?? new AppSettings();
// Product requirement: access tokens always expire after exactly one hour.
appSettings.Jwt.AccessTokenMinutes = 60;
if (string.IsNullOrWhiteSpace(appSettings.Database.ConnectionString))
    throw new InvalidOperationException("Configure App:Database:ConnectionString using user-secrets or environment variables.");
if (appSettings.Jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Configure App:Jwt:SigningKey with at least 32 random characters.");

var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "SysLogs");
Directory.CreateDirectory(logDirectory);
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "swapDigit.API")
    .WriteTo.File(
        path: Path.Combine(logDirectory, "swapDigit-.log"),
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 30,
        buffered: true,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [Trace:{TraceId}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<SqlDataAccess>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "swapDigit API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});
builder.Services.AddCors(options => options.AddPolicy("Clients", policy =>
{
    if (appSettings.Cors.AllowedOrigins.Length > 0) policy.WithOrigins(appSettings.Cors.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = appSettings.Jwt.Issuer,
        ValidateAudience = true, ValidAudience = appSettings.Jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Jwt.SigningKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
    };
    options.GetLevel = (httpContext, elapsed, exception) =>
        exception != null || httpContext.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;
});
app.UseExceptionHandler(error => error.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(exception, "Unhandled API exception. TraceId: {TraceId}", context.TraceIdentifier);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { success = false, message = "An unexpected server error occurred.", traceId = context.TraceIdentifier });
}));
app.UseHttpsRedirection();
app.UseCors("Clients");
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { success = true, service = appSettings.Name, status = "Healthy" }));
app.Run();

public partial class Program;
