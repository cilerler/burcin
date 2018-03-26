Select-Location ".\src\Burcin.Console";
dotnet ef migrations add initial --project ../Burcin.Migrations/;
dotnet ef database update;
