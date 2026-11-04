using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.Middleware;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Repositories;
using RestaurantManagement.API.Services;
using Serilog;
var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json",false,true).AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json",true,true).AddEnvironmentVariables();
var cfg=builder.Configuration.GetSection(AppSettings.SectionName).Get<AppSettings>()??throw new InvalidOperationException("Missing AppSettings.");cfg.Validate(builder.Environment);
Directory.CreateDirectory(cfg.Paths.Logs);Directory.CreateDirectory(cfg.Paths.UserImages);Directory.CreateDirectory(cfg.Paths.Temp);
Log.Logger=new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext().WriteTo.Console().WriteTo.File(Path.Combine(cfg.Paths.Logs,"api-.log"),rollingInterval:RollingInterval.Day,retainedFileCountLimit:cfg.Logging.RetainedFileCount).CreateLogger();builder.Host.UseSerilog();
builder.Services.AddSingleton(cfg);builder.Services.AddSingleton<ISqlConnectionFactory,SqlConnectionFactory>();builder.Services.AddScoped<IAuthRepository,AuthRepository>();builder.Services.AddScoped<IAuthService,AuthService>();builder.Services.AddScoped<ITokenService,TokenService>();builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AuthUser>,Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>>();
var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.Jwt.SigningKey));builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>o.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer=cfg.Jwt.Issuer,ValidateAudience=true,ValidAudience=cfg.Jwt.Audience,ValidateIssuerSigningKey=true,IssuerSigningKey=key,ValidateLifetime=true,ClockSkew=TimeSpan.FromSeconds(30),RoleClaimType="role"});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options=>{
 options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
 options.AddPolicy("auth",context=>RateLimitPartition.GetFixedWindowLimiter(
  partitionKey:context.Connection.RemoteIpAddress?.ToString()??"unknown",
  factory:_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
});
builder.Services.AddControllers();builder.Services.AddEndpointsApiExplorer();builder.Services.AddExceptionHandler<ApiExceptionHandler>();builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen(o=>{o.SwaggerDoc("v1",new OpenApiInfo{Title="Restaurant Management API",Version="v1"});o.AddSecurityDefinition("Bearer",new OpenApiSecurityScheme{Name="Authorization",Type=SecuritySchemeType.Http,Scheme="bearer",BearerFormat="JWT"});o.AddSecurityRequirement(d=>new OpenApiSecurityRequirement{[new OpenApiSecuritySchemeReference("Bearer",d)]=[]});});
builder.Services.AddCors(o=>o.AddPolicy("ConfiguredClients",p=>{if(cfg.AllowedOrigins.Length>0)p.WithOrigins(cfg.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();}));
var app=builder.Build();app.UseExceptionHandler();app.UseSerilogRequestLogging();app.UseHttpsRedirection();app.UseRouting();app.UseCors("ConfiguredClients");app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();if(cfg.Swagger.Enabled){app.UseSwagger();app.UseSwaggerUI();}app.MapControllers();app.MapGet("/health",()=>Results.Ok(new{status="Healthy",service=cfg.ApplicationName})).AllowAnonymous();app.Run();