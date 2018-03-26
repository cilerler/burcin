dotnet ef dbcontext scaffold "data source=localhost;initial catalog=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin" Microsoft.EntityFrameworkCore.SqlServer -f -d -o "..\Burcin.Models\Model" -c "BurcinDbContext" -t MySchema.MyTable1 -t MySchema.MyTable2 --schema MySchema --use-database-names

# 1. Rename `namespace Burcin.Console` to `namespace Burcin.Models.Model` in directory `..\Burcin.Models\Model`
# 2. locate `BurcinDbContext.cs` file under `..\Burcin.Models\Model`
#   a. move it into `Burcin.Data` project
#   b. locate and open the file
#   c. rename `namespace Burcin.Models.Model` to `namespace Burcin.Data`
#   d. add `using Burcin.Models.Model;` to the top
#   e. add `public BurcinDbContext(DbContextOptions options) : base(options) {}`
#   f. clean #warning pragma & server connection string under `protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)`