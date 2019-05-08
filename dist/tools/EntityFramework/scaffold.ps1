#--if (WebApiApplicationExists)
Set-Location ".\src\Burcin.Api";
#--endif

#--if (ConsoleApplicationExists)
Set-Location ".\src\Burcin.Console";
#--endif

dotnet ef dbcontext scaffold "data source=localhost;initial catalog=BurcinDatabase;Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin" Microsoft.EntityFrameworkCore.SqlServer -f -d -o "..\Burcin.Models\BurcinDatabase" -c "BurcinDatabaseDbContext" --schema MySchema -t MySchema.MyTable1 -t MySchema.MyTable2;

Set-Location "..\Burcin.Models\BurcinDatabase";
# 1. Rename all `namespace Burcin.Api` to `namespace Burcin.Models.BurcinDatabase` in directory `..\Burcin.Models\BurcinDatabase`
Get-ChildItem "." -Recurse | ForEach-Object { (Get-Content $_ | ForEach-Object  { $_ -replace "namespace Burcin.Api", "namespace Burcin.Models.BurcinDatabase" }) | Set-Content $_ };
# 2. locate `BurcinDatabaseDbContext.cs` file in directory `..\Burcin.Models\BurcinDatabase`
#   a. add `using Burcin.Models.BurcinDatabase;` to the top
#   b. rename `namespace Burcin.Models.BurcinDatabase` to `namespace Burcin.Data`
(Get-Content "BurcinDatabaseDbContext.cs").replace("namespace Burcin.Models.BurcinDatabase", "using Burcin.Models.BurcinDatabase;`n`nnamespace Burcin.Data") | Set-Content "BurcinDatabaseDbContext.cs";
#   c. clean #warning pragma & server connection string under `protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)`
Set-Content -Path "BurcinDatabaseDbContext.cs" -Value (Get-Content -Path "BurcinDatabaseDbContext.cs" | Select-String -pattern "(#warning)|(optionsBuilder.UseSqlServer)" -notmatch)
#   d. move it into `Burcin.Data` project
Move-Item -Path ".\BurcinDatabaseDbContext.cs" -Destination "..\..\Burcin.Data\BurcinDatabaseDbContext.cs";
