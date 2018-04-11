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
