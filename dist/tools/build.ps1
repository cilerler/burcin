$buildNumber = "0.$((Get-Date).ToString("yyMM.dd.HHmm"))";
dotnet build /p:BuildNumber=$buildNumber -c Release;
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE; }

dotnet pack /p:BuildNumber=$buildNumber -c Release -o "..\nupkgs" --no-build --include-symbols;
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE; }

dotnet publish /p:BuildNumber=$buildNumber -c Release;
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE; }

#--if (DocFx)
& "$PSScriptRoot/documentation.ps1";
#--endif

# === DO NOT REMOVE THIS LINE ===
