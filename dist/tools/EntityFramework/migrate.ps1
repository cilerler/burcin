Set-Location ".\src\Burcin.Host";
dotnet ef migrations add initial --context BurcinDatabaseDbContext --project ../Burcin.Migrations/;
dotnet ef database update --context BurcinDatabaseDbContext;
