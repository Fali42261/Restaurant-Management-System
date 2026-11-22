using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles = "Owner,Manager"), Route("api/v1")]
public sealed class SettingsController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() =>
        Success(await Db.QueryAsync("SELECT SettingKey,SettingValue,UpdatedAt FROM dbo.RestaurantSettings ORDER BY SettingKey"));

    [HttpGet("settings/{key}")]
    public async Task<IActionResult> GetSetting(string key)
    {
        var rows = await Db.QueryAsync(
            "SELECT SettingKey,SettingValue,UpdatedAt FROM dbo.RestaurantSettings WHERE SettingKey=@Key",
            P("@Key", key));
        return rows.Count == 0 ? NotFound() : Success(rows[0]);
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> SetSetting(string key, [FromBody] SettingValue request)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || request.Value is null || request.Value.Length > 4000)
            return BadRequest(new { success = false, message = "Setting key and value are required; key max 100 and value max 4000 characters." });

        await Db.ExecuteAsync(
            "IF EXISTS(SELECT 1 FROM dbo.RestaurantSettings WHERE SettingKey=@Key) " +
            "UPDATE dbo.RestaurantSettings SET SettingValue=@Value,UpdatedAt=SYSUTCDATETIME() WHERE SettingKey=@Key " +
            "ELSE INSERT dbo.RestaurantSettings(SettingKey,SettingValue,UpdatedAt) VALUES(@Key,@Value,SYSUTCDATETIME())",
            P("@Key", key.Trim()), P("@Value", request.Value));
        return Success(null, "Setting saved.");
    }

    [HttpGet("subscriptions/current")]
    public async Task<IActionResult> CurrentSubscription()
    {
        if (CurrentRestaurantId == Guid.Empty || CurrentUserId == Guid.Empty)
            return BadRequest(new { success = false, message = "Sign in and select a restaurant first." });

        var rows = await Db.QueryAsync(
            """
            SELECT TOP (1)
                s.SubscriptionId, s.UserId, s.RestaurantId, s.PlanCode,
                p.Name AS PlanName, s.BillingCycle, s.Amount,
                s.StartsAt, s.ExpiresAt, s.Status, s.CreatedAt
            FROM dbo.Subscriptions s
            LEFT JOIN dbo.SubscriptionPlans p ON p.PlanCode = s.PlanCode
            WHERE s.RestaurantId = @RestaurantId AND s.UserId = @UserId
            ORDER BY s.CreatedAt DESC, s.SubscriptionId DESC
            """,
            P("@RestaurantId", CurrentRestaurantId), P("@UserId", CurrentUserId));

        return Success(rows.FirstOrDefault());
    }

    [HttpGet("subscriptions/plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans() =>
        Success(await Db.QueryAsync(
            "SELECT PlanId,PlanCode,Name,MonthlyPrice,QuarterlyPrice,YearlyPrice,FeaturesJson " +
            "FROM dbo.SubscriptionPlans WHERE IsActive=1 ORDER BY SortOrder"));

    // Payment is not integrated yet. The owner explicitly selects a plan, so this
    // endpoint activates it immediately and retains the selected user/restaurant.
    [HttpPost("subscriptions/request"), Authorize(Roles = "Owner")]
    public async Task<IActionResult> RequestSubscription([FromBody] SubscriptionRequest request)
    {
        if (CurrentRestaurantId == Guid.Empty || CurrentUserId == Guid.Empty)
            return BadRequest(new { success = false, message = "Sign in and select a restaurant first." });

        var planCode = request.PlanCode?.Trim().ToUpperInvariant();
        var cycle = request.BillingCycle?.Trim();
        if (string.IsNullOrWhiteSpace(planCode))
            return BadRequest(new { success = false, message = "Select a subscription plan." });
        if (cycle is not ("Monthly" or "Quarterly" or "Yearly"))
            return BadRequest(new { success = false, message = "Billing cycle must be Monthly, Quarterly or Yearly." });

        var plans = await Db.QueryAsync(
            "SELECT TOP (1) PlanCode,MonthlyPrice,QuarterlyPrice,YearlyPrice " +
            "FROM dbo.SubscriptionPlans WHERE PlanCode=@Code AND IsActive=1",
            P("@Code", planCode));
        if (plans.Count == 0)
            return BadRequest(new { success = false, message = "Selected subscription plan is not available." });

        var plan = plans[0];
        var amount = Convert.ToDecimal(cycle == "Monthly"
            ? plan["MonthlyPrice"]
            : cycle == "Quarterly" ? plan["QuarterlyPrice"] : plan["YearlyPrice"]);
        var months = cycle == "Monthly" ? 1 : cycle == "Quarterly" ? 3 : 12;
        var subscriptionId = Guid.NewGuid();

        await Db.ExecuteAsync(
            """
            SET XACT_ABORT ON;
            BEGIN TRY
                BEGIN TRANSACTION;

                UPDATE dbo.Subscriptions
                SET Status = 'Expired'
                WHERE RestaurantId = @RestaurantId
                  AND Status IN ('Active', 'Trial');

                UPDATE dbo.Subscriptions
                SET Status = 'Cancelled'
                WHERE RestaurantId = @RestaurantId
                  AND Status = 'PendingPayment';

                INSERT dbo.Subscriptions
                    (SubscriptionId, UserId, RestaurantId, PlanCode, BillingCycle, Amount,
                     StartsAt, ExpiresAt, Status, CreatedAt)
                VALUES
                    (@SubscriptionId, @UserId, @RestaurantId, @PlanCode, @BillingCycle, @Amount,
                     SYSUTCDATETIME(), DATEADD(MONTH, @Months, SYSUTCDATETIME()),
                     'Active', SYSUTCDATETIME());

                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """,
            P("@SubscriptionId", subscriptionId),
            P("@UserId", CurrentUserId),
            P("@RestaurantId", CurrentRestaurantId),
            P("@PlanCode", plan["PlanCode"]),
            P("@BillingCycle", cycle),
            P("@Amount", amount),
            P("@Months", months));

        return CreatedResult(new
        {
            subscriptionId,
            userId = CurrentUserId,
            restaurantId = CurrentRestaurantId,
            planCode = plan["PlanCode"],
            billingCycle = cycle,
            amount,
            status = "Active"
        }, "Subscription activated and saved successfully.");
    }
}

public sealed record SettingValue(string Value);
public sealed record SubscriptionRequest(string PlanCode, string BillingCycle);
