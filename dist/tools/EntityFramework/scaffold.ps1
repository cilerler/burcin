# if the database user is not the dbowner, scaffolding will not generate default values as it states here https://github.com/dotnet/efcore/issues/22842
Push-Location $PSScriptRoot
try {
	Remove-Item -Recurse -Force ".\Scaffold";
	New-Item ".\Scaffold" -ItemType directory;
	Set-Location ".\Scaffold";
	dotnet new sln;
	New-Item ".\src" -ItemType directory;
	Set-Location ".\src";
	dotnet new classlib -n BurcinCo.BurcinApp.Data --framework net10.0;
	dotnet new classlib -n BurcinCo.BurcinApp.Models --framework net10.0;
	dotnet add BurcinCo.BurcinApp.Data/BurcinCo.BurcinApp.Data.csproj reference BurcinCo.BurcinApp.Models/BurcinCo.BurcinApp.Models.csproj;
	Remove-Item -Recurse -Force -Path .\* -Include Class1.cs;
	dotnet sln "..\Scaffold.slnx" add ./BurcinCo.BurcinApp.Models/BurcinCo.BurcinApp.Models.csproj;
	dotnet sln "..\Scaffold.slnx" add ./BurcinCo.BurcinApp.Data/BurcinCo.BurcinApp.Data.csproj;
	Set-Location ".\BurcinCo.BurcinApp.Data";
	dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.*;
	Set-Location "..\BurcinCo.BurcinApp.Models";
	dotnet add package Microsoft.EntityFrameworkCore.Abstractions --version 10.*;
	dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.*;
	dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.*;
	dotnet ef dbcontext scaffold "data source=tcp:host.docker.internal,1433;initial catalog=BurcinDatabase;persist security info=True;user id=sa;password=passwordadmin1;MultipleActiveResultSets=True;Connection Timeout=30;Encrypt=True;TrustServerCertificate=False;App=Scaffold" Microsoft.EntityFrameworkCore.SqlServer `
		--force  --no-onconfiguring --data-annotations `
		--context-namespace "BurcinCo.BurcinApp.Data" --context-dir "..\BurcinCo.BurcinApp.Data" --context "BurcinCo.BurcinDatabaseDbContext" `
		--namespace "BurcinCo.BurcinApp.Models" --output-dir ".\BurcinCo.BurcinApp" `
		--schema dbo `
		# --table "non_production.DataFileCopyQueue" `
		;
	dotnet remove package Microsoft.EntityFrameworkCore.Design;
	dotnet remove package Microsoft.EntityFrameworkCore.SqlServer;
}
finally {
	Pop-Location
}
