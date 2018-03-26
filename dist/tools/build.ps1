# BuildNumber=r.yymm.dd.HHmm
dotnet build /p:BuildNumber=0.1803.25.0926 -c Release
dotnet pack /p:BuildNumber=0.1803.25.0926 -c Release -o "..\nupkgs" --no-build --include-symbols