DATABASE SCRIPT ORDER
======================

1. Open SQL Server Management Studio (SSMS).
2. Execute 01_Tables_And_Seed_Data.txt first.
3. Execute 02_Performance_Indexes_Views_Procedures.txt second.
4. Execute 03_Multi_Restaurant_Tenancy.txt third.
5. Execute 04_Orders_POS_Billing.txt fourth.
6. Execute 07_Auth_Tables_And_Procedures.txt fifth. This is the consolidated Auth database script:
   - Creates/repairs dbo.RefreshTokens and dbo.PasswordResetCodes.
   - Adds missing RestaurantId and AttemptCount columns without deleting data.
   - Installs all stored procedures used by AuthDataAccess.cs.
   - Can be re-run safely; it does not drop user or session data.
7. Confirm the Auth tables and dbo.usp_Auth_* procedures exist using the verification result sets printed by script 07.
8. Configure App:Database:ConnectionString using .NET User Secrets.

The third script assigns pre-existing business records to one "Legacy Restaurant" so data is preserved. Review this mapping before production. Each new Owner registration creates an Owner account; the first restaurant is created through the restaurant setup flow. Every API database connection sets the selected RestaurantId from its signed access token; SQL Server row-level security filters and blocks cross-restaurant business data.

Scripts are plain SQL text. The API does not execute them automatically. Run the scripts in the order above. Script 07 is the consolidated and current Auth schema/procedure installer. The older 05_Auth_Register_Owner.txt and 06_Auth_Stored_Procedures.txt are retained for history and should not be run when using script 07.
