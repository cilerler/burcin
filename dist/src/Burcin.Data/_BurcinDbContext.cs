using Microsoft.EntityFrameworkCore;
using Burcin.Models;

namespace Burcin.Data
{
    public partial class BurcinDbContext : DbContext
    {
        public BurcinDbContext(DbContextOptions<BurcinDbContext> options) : base(options)
        {
        }
    }
}

// namespace Burcin.Data
// {
//     public partial class BurcinDbContext : DbContext
//     {
//         public virtual DbSet<T> T { get; set; }

//         protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//         {
//             if (!optionsBuilder.IsConfigured)
//             {
//             }
//         }

//         protected override void OnModelCreating(ModelBuilder modelBuilder)
//         {
//         }
//     }
// }
