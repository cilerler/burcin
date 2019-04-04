$buildNumber = "0.$((Get-Date).ToString("yyMM.dd.HHmm"))";
dotnet build /p:BuildNumber=$buildNumber -c Release;
dotnet pack /p:BuildNumber=$buildNumber -c Release -o "..\nupkgs" --no-build --include-symbols;

#--if (WindowsService)
dotnet publish /p:BuildNumber=$buildNumber -c Release --runtime win7-x64;
#--else
dotnet publish /p:BuildNumber=$buildNumber -c Release;
#--endif

#--if (DocFx)
docfx docs\docfx\docfx.json --serve;
#--endif

# === DO NOT REMOVE THIS LINE ===
