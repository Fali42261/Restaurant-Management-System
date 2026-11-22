/*
  swapDigit subscription activation support
  Database: swapDigitDb
  Run this script once in SQL Server Management Studio.
  It does not recreate SubscriptionPlans or Subscriptions.
*/
USE swapDigitDb;
GO

IF OBJECT_ID(N'dbo.SubscriptionPlans', N'U') IS NULL
    THROW 51000, 'dbo.SubscriptionPlans is missing. Run the main swapDigit database installer first.', 1;
IF OBJECT_ID(N'dbo.Subscriptions', N'U') IS NULL
    THROW 51001, 'dbo.Subscriptions is missing. Run the main swapDigit database installer first.', 1;
GO

/* Ensure the request/activation metadata columns exist on older installations. */
IF COL_LENGTH('dbo.Subscriptions', 'BillingCycle') IS NULL
    ALTER TABLE dbo.Subscriptions ADD BillingCycle NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Subscriptions', 'Amount') IS NULL
    ALTER TABLE dbo.Subscriptions ADD Amount DECIMAL(18,2) NULL;
IF COL_LENGTH('dbo.Subscriptions', 'PaymentReference') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PaymentReference NVARCHAR(120) NULL;
GO

/* Keep the three plan records available. Existing records are updated to the
   database's current price list; no duplicate plans are inserted. */
UPDATE dbo.SubscriptionPlans
SET Name = N'Basic',
    MonthlyPrice = 499,
    QuarterlyPrice = 1299,
    YearlyPrice = 4999,
    FeaturesJson = N'["POS Billing","Orders","Tables","Basic Reports"]',
    SortOrder = 1,
    IsActive = 1
WHERE PlanCode = N'BASIC';

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE PlanCode = N'BASIC')
    INSERT dbo.SubscriptionPlans
        (PlanId, PlanCode, Name, MonthlyPrice, QuarterlyPrice, YearlyPrice, FeaturesJson, SortOrder, IsActive)
    VALUES
        (NEWID(), N'BASIC', N'Basic', 499, 1299, 4999,
         N'["POS Billing","Orders","Tables","Basic Reports"]', 1, 1);

UPDATE dbo.SubscriptionPlans
SET Name = N'Professional',
    MonthlyPrice = 999,
    QuarterlyPrice = 2699,
    YearlyPrice = 9999,
    FeaturesJson = N'["Everything in Basic","Inventory","Reservations","KOT","Advanced Reports"]',
    SortOrder = 2,
    IsActive = 1
WHERE PlanCode = N'PRO';

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE PlanCode = N'PRO')
    INSERT dbo.SubscriptionPlans
        (PlanId, PlanCode, Name, MonthlyPrice, QuarterlyPrice, YearlyPrice, FeaturesJson, SortOrder, IsActive)
    VALUES
        (NEWID(), N'PRO', N'Professional', 999, 2699, 9999,
         N'["Everything in Basic","Inventory","Reservations","KOT","Advanced Reports"]', 2, 1);

UPDATE dbo.SubscriptionPlans
SET Name = N'Business',
    MonthlyPrice = 1999,
    QuarterlyPrice = 5399,
    YearlyPrice = 19999,
    FeaturesJson = N'["Everything in Professional","Staff Management","Priority Support"]',
    SortOrder = 3,
    IsActive = 1
WHERE PlanCode = N'BUSINESS';

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE PlanCode = N'BUSINESS')
    INSERT dbo.SubscriptionPlans
        (PlanId, PlanCode, Name, MonthlyPrice, QuarterlyPrice, YearlyPrice, FeaturesJson, SortOrder, IsActive)
    VALUES
        (NEWID(), N'BUSINESS', N'Business', 1999, 5399, 19999,
         N'["Everything in Professional","Staff Management","Priority Support"]', 3, 1);
GO

/* Supports fast current-subscription lookups by authenticated owner and restaurant. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Subscriptions')
      AND name = N'IX_Subscriptions_Restaurant_User_Created'
)
    CREATE INDEX IX_Subscriptions_Restaurant_User_Created
    ON dbo.Subscriptions(RestaurantId, UserId, CreatedAt DESC)
    INCLUDE(PlanCode, BillingCycle, Amount, StartsAt, ExpiresAt, Status);
GO

SELECT PlanCode, Name, MonthlyPrice, QuarterlyPrice, YearlyPrice, IsActive
FROM dbo.SubscriptionPlans
ORDER BY SortOrder;
GO

PRINT 'swapDigit subscription setup completed. Existing subscription tables were preserved.';
GO
