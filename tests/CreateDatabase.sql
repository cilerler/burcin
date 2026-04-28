ALTER DATABASE WideWorldImporters SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
GO
DROP DATABASE  WideWorldImporters;
GO
-- CREATE DATABASE WideWorldImporters;
-- GO
RESTORE DATABASE WideWorldImporters FROM DISK = N'/var/opt/mssql/data/WideWorldImporters-Full.bak' WITH REPLACE -- AdventureWorks2022.bak
GO
ALTER DATABASE WideWorldImporters SET MULTI_USER;
GO

-- Check if the database is created and accessible
-- SELECT name, user_access_desc FROM sys.databases WHERE name = 'WideWorldImporters';
