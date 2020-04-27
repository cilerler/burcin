using System;
using System.Reflection;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Burcin.Models;

namespace Burcin.Data
{
    public static class ModelBuilderExtensions
    {
       public static void ShadowProperties(this ModelBuilder modelBuilder)
       {
           foreach (var entityType in modelBuilder.Model.GetEntityTypes())
           {
               Type type = entityType.ClrType;

               if (typeof(ISoftDelete).IsAssignableFrom(type))
               {
                   var method = SetSoftDeleteShadowPropertyMethodInfo.MakeGenericMethod(type);
                   method.Invoke(modelBuilder, new object[] { modelBuilder });
               }

               if (typeof(IAuditable).IsAssignableFrom(type))
               {
                   var method = SetAuditingShadowPropertiesMethodInfo.MakeGenericMethod(type);
                   method.Invoke(modelBuilder, new object[] { modelBuilder });
               }
           }
       }

       private static readonly MethodInfo SetSoftDeleteShadowPropertyMethodInfo = typeof(ModelBuilderExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
           .Single(t => t.IsGenericMethod && t.Name == nameof(SetSoftDeleteShadowProperty));


       private static readonly MethodInfo SetAuditingShadowPropertiesMethodInfo = typeof(ModelBuilderExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
           .Single(t => t.IsGenericMethod && t.Name == nameof(SetAuditingShadowProperties));

       public static void SetSoftDeleteShadowProperty<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
       {
           modelBuilder.Entity<T>().Property<bool>(Constants.SoftDelete).HasDefaultValue(false);
       }

       public static void SetAuditingShadowProperties<T>(ModelBuilder modelBuilder) where T : class, IAuditable
       {

           modelBuilder.Entity<T>().Property<DateTime>(Constants.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
           modelBuilder.Entity<T>().Property<DateTime>(Constants.ModifiedAt).HasDefaultValueSql("GETUTCDATE()");
       }
    }
}
