# Steps

- createSolutionFile.ps1
<!--#if (CacheSqlServer)-->
- sql-cache.ps1
    1. create database `DistributedCache`
    2. create schema `cache`
    3. run the script
<!--#endif-->
- user-secrets.ps1
<!--#if (EntityFramework)-->
- scaffold.ps1
- migrate.ps1
<!--#endif-->
- build.ps1
<!--#if (WindowsService)-->
- windows-service.ps1
<!--#endif-->

=== DO NOT REMOVE THIS LINE ===
