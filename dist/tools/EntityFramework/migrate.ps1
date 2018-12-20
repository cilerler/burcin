#--if (WebApiApplicationExists)
Set-Location ".\src\Burcin.Api";
#--endif

#--if (ConsoleApplicationExists)
Set-Location ".\src\Burcin.Console";
#--endif

dotnet ef migrations add initial --project ../Burcin.Migrations/;
dotnet ef database update;
