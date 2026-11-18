using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager"), Route("api/v1")]
public sealed class SettingsController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()=>Success(await Db.QueryAsync("SELECT SettingKey,SettingValue,UpdatedAt FROM dbo.RestaurantSettings ORDER BY SettingKey"));

    [HttpGet("settings/{key}")]
    public async Task<IActionResult> GetSetting(string key)
    {
        var rows=await Db.QueryAsync("SELECT SettingKey,SettingValue,UpdatedAt FROM dbo.RestaurantSettings WHERE SettingKey=@Key",P("@Key",key));
        return rows.Count==0?NotFound():Success(rows[0]);
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> SetSetting(string key,[FromBody]SettingValue request)
    {
        if(string.IsNullOrWhiteSpace(key)||key.Length>100||request.Value is null||request.Value.Length>4000)
            return BadRequest(new {success=false,message="Setting key and value are required; key max 100 and value max 4000 characters."});
        await Db.ExecuteAsync("IF EXISTS(SELECT 1 FROM dbo.RestaurantSettings WHERE SettingKey=@Key) UPDATE dbo.RestaurantSettings SET SettingValue=@Value,UpdatedAt=SYSUTCDATETIME() WHERE SettingKey=@Key ELSE INSERT dbo.RestaurantSettings(SettingKey,SettingValue,UpdatedAt) VALUES(@Key,@Value,SYSUTCDATETIME())",P("@Key",key.Trim()),P("@Value",request.Value));
        return Success(null,"Setting saved.");
    }

    [HttpGet("subscriptions/current")]
    public async Task<IActionResult> CurrentSubscription()
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        var rows=await Db.QueryAsync("SELECT TOP(1) SubscriptionId,PlanCode,BillingCycle,Amount,StartsAt,ExpiresAt,Status,CreatedAt FROM dbo.Subscriptions ORDER BY CreatedAt DESC");
        return Success(rows.FirstOrDefault());
    }

    [HttpGet("subscriptions/plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans()=>Success(await Db.QueryAsync("SELECT PlanId,PlanCode,Name,MonthlyPrice,QuarterlyPrice,YearlyPrice,FeaturesJson FROM dbo.SubscriptionPlans WHERE IsActive=1 ORDER BY SortOrder"));

    [HttpPost("subscriptions/request"),Authorize(Roles="Owner")]
    public async Task<IActionResult> RequestSubscription(SubscriptionRequest request)
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        var cycle=request.BillingCycle?.Trim();
        if(cycle is not ("Monthly" or "Quarterly" or "Yearly")) return BadRequest(new {success=false,message="Billing cycle must be Monthly, Quarterly or Yearly."});
        var plans=await Db.QueryAsync("SELECT TOP(1) PlanCode,MonthlyPrice,QuarterlyPrice,YearlyPrice FROM dbo.SubscriptionPlans WHERE PlanCode=@Code AND IsActive=1",P("@Code",request.PlanCode?.Trim()));
        if(plans.Count==0) return BadRequest(new {success=false,message="Selected subscription plan is not available."});
        var pending=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Subscriptions WHERE Status='PendingPayment'");
        if(Convert.ToInt32(pending)>0) return Conflict(new {success=false,message="A subscription request is already pending for this restaurant."});
        var plan=plans[0];
        var amount=Convert.ToDecimal(cycle=="Monthly"?plan["MonthlyPrice"]:cycle=="Quarterly"?plan["QuarterlyPrice"]:plan["YearlyPrice"]);
        var duration=cycle=="Monthly"?1:cycle=="Quarterly"?3:12;
        var id=Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.Subscriptions(SubscriptionId,UserId,PlanCode,BillingCycle,Amount,StartsAt,ExpiresAt,Status,CreatedAt) VALUES(@Id,@User,@Plan,@Cycle,@Amount,SYSUTCDATETIME(),DATEADD(MONTH,@Months,SYSUTCDATETIME()),'PendingPayment',SYSUTCDATETIME())",
            P("@Id",id),P("@User",CurrentUserId),P("@Plan",plan["PlanCode"]),P("@Cycle",cycle),P("@Amount",amount),P("@Months",duration));
        return CreatedResult(new {subscriptionId=id,planCode=plan["PlanCode"],billingCycle=cycle,amount,status="PendingPayment",restaurantId=CurrentRestaurantId},
            "Subscription request created for this restaurant only. Payment activation is not enabled yet.");
    }
}
public sealed record SettingValue(string Value);
public sealed record SubscriptionRequest(string PlanCode,string BillingCycle);
