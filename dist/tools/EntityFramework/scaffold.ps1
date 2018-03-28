dotnet ef dbcontext scaffold "data source=localhost;initial catalog=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin" Microsoft.EntityFrameworkCore.SqlServer -f -d -o "..\Burcin.Models\(databaseName)" -c "BurcinDbContext" --use-database-names --schema MySchema -t MySchema.MyTable1 -t MySchema.MyTable2

# 1. Rename `namespace Burcin.Console` to `namespace Burcin.Models.(databaseName)` in directory `..\Burcin.Models\(databaseName)`
# 2. locate `BurcinDbContext.cs` file in directory `..\Burcin.Models\(databaseName)`
#   a. move it into `Burcin.Data` project
#   b. locate and open the file
#   c. rename `namespace Burcin.Models.(databaseName)` to `namespace Burcin.Data`
#   d. add `using Burcin.Models.(databaseName);` to the top
#   e. add `public BurcinDbContext(DbContextOptions options) : base(options) {}`
#   f. clean #warning pragma & server connection string under `protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)`