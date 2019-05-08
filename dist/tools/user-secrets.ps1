# Secrets location %APPDATA%\microsoft\UserSecrets\<userSecretsId>\secrets.json

#--if (WebApiApplicationExists)
dotnet user-secrets --project ".\src\Burcin.Api" set ConnectionStrings:DefaultConnection "data source=localhost;initial catalog=BurcinDatabase;Trusted_Connection=True;MultipleActiveResultSets=true;App=Burcin.Api";
#--endif

#--if (ConsoleApplicationExists)
Set-Location ".\src\Burcin.Console";
dotnet user-secrets set ConnectionStrings:DefaultConnection "data source=localhost;initial catalog=BurcinDatabase;Trusted_Connection=True;MultipleActiveResultSets=True;App=Burcin.Console";
#--endif

# === DO NOT REMOVE THIS LINE ===
