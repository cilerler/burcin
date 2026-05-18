-- ============================================================
-- Soft-delete trigger for the single ISoftDelete demo entity (Recipe.Chef).
--
-- The template intentionally ships ONE soft-delete example rather than retrofitting it onto
-- every entity. Chef was picked because the narrative is clean ("a chef leaves but historical
-- recipes still FK to them"), the AppHost.E2E suite already round-trips through DELETE on it,
-- and it carries the filtered IX_Chef_SoftDelete_ModifiedAt index showing the "active rows"
-- query shape. To add soft-delete to a new entity:
--   1. Implement ISoftDelete on the entity's BurcinDatabaseExtend partial.
--   2. Add `public bool SoftDelete { get; set; }` to the BurcinDatabase partial.
--   3. Add a CREATE OR ALTER TRIGGER block here following the Chef pattern.
--   4. Run migrate.ps1 to regenerate + apply.
-- The global query filter on ISoftDelete entities and the SaveChangesInterceptor are
-- registered generically in Data — no per-entity wiring required.
--
-- INSTEAD OF DELETE: a `DELETE FROM Recipe.Chef` statement is intercepted and converted into
-- `UPDATE SET SoftDelete = 1`.
--
-- This trigger works alongside the app-side SoftDeleteSaveChangesInterceptor in
-- BurcinCo.BurcinApp.Data — belt-and-suspenders, not redundant:
--   * The interceptor catches the EF tracker path (`_db.X.Remove(entity) + SaveChangesAsync`).
--     Required because EF Core's generated DELETE includes an OUTPUT clause for the
--     optimistic-concurrency rowcount check, and SQL Server error 334 forbids OUTPUT
--     (without INTO) when the target table has a trigger. Converting at the EF level
--     means the SQL sent is UPDATE, not DELETE — no trigger conflict, concurrency stays
--     intact.
--   * The trigger catches raw-SQL delete paths the interceptor can't see: ad-hoc
--     `ExecuteSqlRawAsync("DELETE FROM ...")`, maintenance scripts, sqlcmd sessions,
--     other services on the same database. The chokepoint at the DB ensures the
--     soft-delete invariant survives even when the app is bypassed.
--
-- Test cleanup pattern (for fixtures that need to hard-delete between tests):
--   DISABLE TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef];
--   DELETE FROM [Recipe].[Chef];
--   ENABLE TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef];
-- The DISABLE bypasses the trigger; the interceptor doesn't see ExecuteSqlRawAsync either, so
-- DELETE FROM under DISABLE TRIGGER produces a real hard delete.
--
-- Re-runnable: CREATE OR ALTER means migrate.ps1 can apply this after every
-- `dotnet ef database update` without erroring on existing triggers.
--
-- DB-name-agnostic: no USE statement. The connection's current database determines where the
-- trigger lands. migrate.ps1 passes -d <DatabaseName> to sqlcmd; test fixtures' Testcontainer
-- connections default to master (which is where EF migrates the schema in tests).
-- ============================================================

CREATE OR ALTER TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef]
    INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE entity
    SET entity.SoftDelete = 1
    FROM [Recipe].[Chef] AS entity
    INNER JOIN DELETED AS d ON entity.Id = d.Id;
END
GO
