using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using BurcinCo.BurcinApp.Models.Abstractions;
#if (Sample)
using Ruya.Services.ReliableMessaging.EntityFrameworkCore;
using ModelsConstants = BurcinCo.BurcinApp.Models.BurcinDatabase.Constants;
#endif

namespace BurcinCo.BurcinApp.Data
{
	public partial class BurcinDatabaseDbContext : DbContext
	{
		private void OnModelCreatingPostActions(ModelBuilder modelBuilder)
		{
			SetGlobalQueryFilters(modelBuilder);
			modelBuilder.ApplyConfigurationsFromAssembly(typeof(BurcinDatabaseDbContext).Assembly);

			#if (Sample)
			// Outbox + Inbox schema lives in Data because it's persistence infrastructure shared across modules.
			// Hardcoded "dbo" matches the existing migration; any change needs to flow through migrate.ps1 regen.
			modelBuilder.ApplyOutboxEntryConfiguration(new EntityFrameworkOutboxStoreOptions { SchemaName = ModelsConstants.DefaultSchema });
			modelBuilder.ApplyInboxEntryConfiguration(new EntityFrameworkInboxStoreOptions { SchemaName = ModelsConstants.DefaultSchema });
			#endif
		}

		private void SetGlobalQueryFilters(ModelBuilder modelBuilder)
		{
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
				{
					var method = _setGlobalQueryForSoftDeleteMethodInfo.MakeGenericMethod(entityType.ClrType);
					method.Invoke(this, new object[] { modelBuilder });
				}
			}
		}

		public void SetGlobalQueryForSoftDelete<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
		{
			modelBuilder.Entity<T>().HasQueryFilter(item => !EF.Property<bool>(item, nameof(ISoftDelete.SoftDelete)));
		}

		private readonly MethodInfo _setGlobalQueryForSoftDeleteMethodInfo = typeof(BurcinDatabaseDbContext).GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Single(t => t.IsGenericMethod && t.Name == nameof(SetGlobalQueryForSoftDelete));
	}
}
