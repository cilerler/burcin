using System;
using System.Reflection;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Burcin.Models;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Burcin.Data
{
	public static class ModelBuilderExtensions
	{
		public static void ShadowProperties(this ModelBuilder modelBuilder)
		{
			foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
			{
				Type type = entityType.ClrType;

				if (typeof(ISoftDelete).IsAssignableFrom(type))
				{
					MethodInfo method = SetSoftDeleteShadowPropertyMethodInfo.MakeGenericMethod(type);
					method.Invoke(modelBuilder, new object[] {modelBuilder});
				}

				if (typeof(ITimestamp).IsAssignableFrom(type))
				{
					MethodInfo method = SetAuditingShadowPropertiesMethodInfo.MakeGenericMethod(type);
					method.Invoke(modelBuilder, new object[] {modelBuilder});
				}

				if (typeof(IBaseEntity).IsAssignableFrom(type))
				{
					MethodInfo method = SetBaseEntityShadowPropertiesMethodInfo.MakeGenericMethod(type);
					method.Invoke(modelBuilder, new object[] { modelBuilder });
				}
			}
		}

		private static readonly MethodInfo SetSoftDeleteShadowPropertyMethodInfo = typeof(ModelBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(t => t.IsGenericMethod && t.Name == nameof(SetSoftDeleteShadowProperty));


		private static readonly MethodInfo SetAuditingShadowPropertiesMethodInfo = typeof(ModelBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(t => t.IsGenericMethod && t.Name == nameof(SetTimestampShadowProperties));

		private static readonly MethodInfo SetBaseEntityShadowPropertiesMethodInfo = typeof(ModelBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(t => t.IsGenericMethod && t.Name == nameof(SetBaseEntityShadowProperties));

		public static void SetSoftDeleteShadowProperty<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
		{
			modelBuilder.Entity<T>().Property<bool>(Constants.SoftDelete).HasDefaultValue(false).ValueGeneratedOnAdd();
		}

		public static void SetTimestampShadowProperties<T>(ModelBuilder modelBuilder) where T : class, ITimestamp
		{
			modelBuilder.Entity<T>().Property<DateTime>(Constants.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()")
				.ValueGeneratedOnAdd();
			modelBuilder.Entity<T>().Property<DateTime>(Constants.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()")
				.ValueGeneratedOnAddOrUpdate();
		}

		public static void SetBaseEntityShadowProperties<T>(ModelBuilder modelBuilder) where T : class, IBaseEntity
		{
			modelBuilder.Entity<T>().Property<Guid>(Constants.RowGuid).IsConcurrencyToken()
				.HasDefaultValueSql("NEWID()").ValueGeneratedOnAddOrUpdate();
			modelBuilder.Entity<T>().Property<byte[]>(Constants.RowVersion).IsRowVersion()
				.ValueGeneratedOnAddOrUpdate();
		}
	}
}
