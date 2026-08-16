Push-Location $PSScriptRoot
try {
	Set-Location ".\..\..\src\BurcinCo.BurcinApp.Migrations";
	dotnet ef migrations add initial --context BurcinDatabaseDbContext --project . --startup-project .;
	dotnet ef database update --context BurcinDatabaseDbContext --project . --startup-project .;

	# Apply post-migration triggers. EF Core doesn't author triggers (it owns schema, not behaviour
	# bolted onto schema), so anything DDL-but-not-EF lives here. Currently: the soft-delete
	# INSTEAD OF DELETE triggers in triggers.sql — see that file's header for the layering rationale.
	# Run via `docker exec mssql sqlcmd` because sqlcmd isn't on the host PATH; the persistent
	# mssql container has it at /opt/mssql-tools18/bin/sqlcmd. `-b` makes sqlcmd return non-zero on
	# SQL errors so PowerShell catches them.
	$triggersSqlPath = Join-Path $PSScriptRoot "triggers.sql";
	$containerTmpPath = "/tmp/burcinapp-triggers.sql";
	# `BurcinDatabase` is template-substituted to the user's --DatabaseName at generation time.
	$databaseName = "BurcinDatabase";
	docker cp $triggersSqlPath "mssql:$containerTmpPath";
	if ($LASTEXITCODE -ne 0) { throw "Copy triggers.sql to mssql container failed (exit $LASTEXITCODE). Is the 'mssql' container running?"; }
	docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,1433" -U sa -P "PasswordAdmin1!" -C -b -d $databaseName -i $containerTmpPath;
	if ($LASTEXITCODE -ne 0) { throw "triggers.sql execution failed with exit code $LASTEXITCODE."; }
}
finally {
	Pop-Location
}
