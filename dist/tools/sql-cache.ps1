# dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" dbo DotNetCache

##if (CacheExists)
dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" cache BurcinApi
##if (ConsoleApplication)
dotnet sql-cache create "data source=localhost;initial catalog=DistributedCache;Integrated Security=True;" cache BurcinConsole
##endif
##endif

