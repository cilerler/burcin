# Secrets location
# %APPDATA%\microsoft\UserSecrets\<userSecretsId>\secrets.json

Set-Location ".\src\Burcin.Console";
dotnet user-secrets set ConnectionStrings:DefaultConnection "data source=localhost;initial catalog=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin.Console"

dotnet user-secrets --project "..\Burcin.Api" set ConnectionStrings:DefaultConnection "Server=(localdb)\\mssqllocaldb;Database=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=true;App=Burcin.Api"
