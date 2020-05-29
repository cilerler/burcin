# Secrets location %APPDATA%\microsoft\UserSecrets\<userSecretsId>\secrets.json

dotnet user-secrets --project ".\src\Burcin.Host" set ConnectionStrings:MsSqlConnection "data source=localhost;initial catalog=BurcinDatabase;Trusted_Connection=True;MultipleActiveResultSets=true;App=Burcin.Host";

# === DO NOT REMOVE THIS LINE ===
