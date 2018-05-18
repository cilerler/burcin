# 1. Rename all `namespace Burcin.Api` to `namespace Burcin.Models.(databaseName)` in directory `..\Burcin.Models\(databaseName)`
# 2. locate `BurcinDbContext.cs` file in directory `..\Burcin.Models\(databaseName)`
#   a. clean #warning pragma & server connection string under `protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)`
#   b. rename `namespace Burcin.Models.(databaseName)` to `namespace Burcin.Data`
#   c. add `using Burcin.Models.(databaseName);` to the top
#   d. move it into `Burcin.Data` project

Set-Location ".\src\Burcin.Api";
dotnet ef dbcontext scaffold "data source=localhost;initial catalog=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin" Microsoft.EntityFrameworkCore.SqlServer -f -d -o "..\Burcin.Models\(databaseName)" -c "BurcinDbContext" --schema MySchema -t MySchema.MyTable1 -t MySchema.MyTable2;
Set-Location "..\Burcin.Models\(databaseName)";
Get-ChildItem "." -Recurse | ForEach-Object { (Get-Content $_ | ForEach-Object  { $_ -replace "namespace Burcin.Api", "namespace Burcin.Models.(databaseName)" }) | Set-Content $_ };
(Get-Content "BurcinDbContext.cs").replace("namespace Burcin.Models.(databaseName)", "using Burcin.Models.(databaseName);`n`nnamespace Burcin.Data") | Set-Content "BurcinDbContext.cs";
Set-Content -Path "BurcinDbContext.cs" -Value (Get-Content -Path "BurcinDbContext.cs" | Select-String -pattern "(#warning)|(optionsBuilder.UseSqlServer)" -notmatch)
Move-Item -Path ".\BurcinDbContext.cs" -Destination "..\..\Burcin.Data\BurcinDbContext.cs";
