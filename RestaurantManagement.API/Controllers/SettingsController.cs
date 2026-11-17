using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager"), Route("api/v1")]
public sealed class SettingsController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("settings")] public async Task<IActionResult> GetSettings() => Success(await Db.QueryAsync("SELECT SettingKey,SettingValue,UpdatedAt FROM dbo.RestaurantSettings ORDER BY SettingKey"));
    [HttpPut("settings/{key}")] public async Task<IActionResult> SetSetting(string key,[FromBody] SettingValue request)
    {
        await Db.ExecuteAsync("IF EXISTS(SELECT 1 FROM dbo.RestaurantSettings WHERE SettingKey=@Key) UPDATE dbo.RestaurantSettings SET SettingValue=@Value,UpdatedAt=SYSUTCDATETIME() WHERE SettingKey=@Key ELSE INSERT dbo.RestaurantSettings(SettingKey,SettingValue,UpdatedAt) VALUES(@Key,@Value,SYSUTCDATETIME())",P("@Key",key),P("@Value",request.Value));
        return Success(null,"Setting saved.");
    }
    [HttpGet("subscriptions/current")] public async Task<IActionResult> CurrentSubscription() => Success((await Db.QueryAsync("SELECT TOP 1 SubscriptionId,PlanCode,StartsAt,ExpiresAt,Status FROM dbo.Subscriptions WHERE UserId=@Id ORDER BY CreatedAt DESC",P("@Id",CurrentUserId))).FirstOrDefault());
    [HttpGet("subscriptions/plans")] public async Task<IActionResult> Plans() => Success(await Db.QueryAsync("SELECT PlanId,PlanCode,Name,MonthlyPrice,QuarterlyPrice,YearlyPrice,FeaturesJson FROM dbo.SubscriptionPlans WHERE IsActive=1 ORDER BY SortOrder"));
}
public sealed record SettingValue(string Value);