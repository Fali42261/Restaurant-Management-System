DATABASE SCRIPT ORDER
======================

1. Open SQL Server Management Studio (SSMS).
2. Execute 01_Tables_And_Seed_Data.txt first.
3. Execute 02_Performance_Indexes_Views_Procedures.txt second.
4. Confirm swapDigitDb and its objects exist.
5. Configure App:Database:ConnectionString using .NET User Secrets.

Scripts are plain SQL text. The API does not execute them automatically.
