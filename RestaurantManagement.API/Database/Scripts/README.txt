DATABASE SCRIPT ORDER
======================

1. Open SQL Server Management Studio (SSMS).
2. Execute 01_Tables_And_Seed_Data.txt first.
3. Execute 02_Performance_Indexes_Views_Procedures.txt second.
4. Execute 03_Multi_Restaurant_Tenancy.txt third.
5. Confirm swapDigitDb contains Restaurants, UserRestaurants and tenant-scoped business tables.
6. Configure App:Database:ConnectionString using .NET User Secrets.

The third script assigns pre-existing business records to one "Legacy Restaurant" so data is preserved. Review this mapping before production. Each new Owner registration creates its own first restaurant. Every API database connection sets the selected RestaurantId from its signed access token; SQL Server row-level security filters and blocks cross-restaurant business data.

Scripts are plain SQL text. The API does not execute them automatically. Run them in the order above.
