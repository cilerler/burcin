# Secrets location
# %APPDATA%\microsoft\UserSecrets\<userSecretsId>\secrets.json

#//#if (ConsoleApplication)
Set-Location ".\src\Burcin.Console";
dotnet user-secrets set ConnectionStrings:DefaultConnection "data source=localhost;initial catalog=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin.Console"
#//#endif

dotnet user-secrets --project ".\src\Burcin.Api" set ConnectionStrings:DefaultConnection "data source=localhost;initial catalog=(databaseName);Trusted_Connection=True;MultipleActiveResultSets=true;App=Burcin.Api"
