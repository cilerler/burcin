using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using BurcinCo.BurcinApp.Models.Abstractions;

namespace BurcinCo.BurcinApp.Data;

/// <summary>
/// Converts EF-tracked deletes of <see cref="ISoftDelete"/> entities into UPDATEs that set
/// <c>SoftDelete = true</c>. Works alongside the DB-side <c>INSTEAD OF DELETE</c> triggers in
/// <c>tools/EntityFramework/triggers.sql</c> as belt-and-suspenders:
/// <list type="bullet">
///   <item><b>This interceptor</b> catches the EF-tracker path (<c>_db.X.Remove(entity) + SaveChangesAsync</c>).
///         Required because EF Core's generated DELETE includes an <c>OUTPUT</c> clause for the
///         optimistic-concurrency rowcount check, and SQL Server error 334 forbids <c>OUTPUT</c>
///         (without <c>INTO</c>) when the target table has a trigger. Converting at the EF level
///         means the SQL sent is UPDATE, not DELETE — no trigger conflict, concurrency stays intact.</item>
///   <item><b>The triggers</b> catch raw-SQL paths (maintenance scripts, sqlcmd, other services
///         on the same DB). The chokepoint is at the DB; the interceptor doesn't see those.</item>
/// </list>
/// Modules that issue ad-hoc <c>ExecuteSqlRawAsync("DELETE FROM ...")</c> are also covered by the
/// triggers (the interceptor only fires for tracker-mediated changes). Test fixtures that need
/// hard-delete for cleanup <c>DISABLE TRIGGER</c> around their <c>DELETE FROM</c> statements.
/// </summary>
public sealed class SoftDeleteSaveChangesInterceptor : SaveChangesInterceptor
{
	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		ApplySoftDelete(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override System.Threading.Tasks.ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData, InterceptionResult<int> result, System.Threading.CancellationToken cancellationToken = default)
	{
		ApplySoftDelete(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private static void ApplySoftDelete(DbContext? context)
	{
		if (context is null) return;
		foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDelete))
		{
			entry.State = EntityState.Modified;
			entry.Property(nameof(ISoftDelete.SoftDelete)).CurrentValue = true;
		}
	}
}
