# dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" dbo DotNetCache

#--if (CacheExists)
dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" cache BurcinHost
#--endif

# === DO NOT REMOVE THIS LINE ===
