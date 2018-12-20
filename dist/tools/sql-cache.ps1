# dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" dbo DotNetCache

#--if (CacheExists)

#--if (WebApiApplicationExists)
dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" cache BurcinApi
#--endif

#--if (ConsoleApplicationExists)
dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" cache BurcinConsole
#--endif

#--endif

