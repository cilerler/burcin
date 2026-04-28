Push-Location $PSScriptRoot
try {
	Set-Location ".\..\..\src\BurcinCo.BurcinApp.Host";
	dotnet ef migrations add initial --context BurcinDatabaseDbContext --project ../BurcinCo.BurcinApp.Migrations/;
	dotnet ef database update --context BurcinDatabaseDbContext;
}
finally {
	Pop-Location
}
