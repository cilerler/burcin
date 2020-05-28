using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Burcin.Models;

namespace Burcin.Data
{
    public static class ChangeTrackerExtensions
    {
       public static void SetShadowProperties(this ChangeTracker changeTracker)
       {
           changeTracker.DetectChanges();

           foreach (var entry in changeTracker.Entries())
           {
               var timestamp = DateTime.UtcNow;
               if (entry.Entity is IAuditable)
               {
                   if (entry.State == EntityState.Added)
                   {
                       entry.Property(Constants.CreatedAt).CurrentValue = timestamp;
                   }
                   if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                   {
                       entry.Property(Constants.ModifiedAt).CurrentValue = timestamp;
                   }
               }

               if (entry.Entity is ISoftDelete && entry.State == EntityState.Deleted)
               {
                   entry.State = EntityState.Modified;
                   entry.Property(Constants.SoftDelete).CurrentValue = true;
               }
           }
       }
    }
}
