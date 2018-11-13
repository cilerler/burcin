$buildNumber = "0.$((Get-Date).ToString("yyMM.dd.HHmm"))";
dotnet build /p:BuildNumber=$buildNumber -c Release;
dotnet pack /p:BuildNumber=$buildNumber -c Release -o "..\nupkgs" --no-build --include-symbols;
#//#if (!WindowsService)
dotnet publish /p:BuildNumber=$buildNumber -c Release;
#//#else
dotnet publish /p:BuildNumber=$buildNumber -c Release --runtime win7-x64;
#//#endif
