Set-Location ".\src\Burcin.Api";
dotnet ef migrations add initial --project ../Burcin.Migrations/;
dotnet ef database update;
