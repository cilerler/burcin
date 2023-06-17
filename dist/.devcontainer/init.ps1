$bashProfilePath = "$HOME/.bash_profile"
$dotnetToolsPath = "$HOME/.dotnet/tools"

Add-Content -Path $bashProfilePath -Value @"
# Add .NET Core SDK tools
Export-Path $env:Path;$dotnetToolsPath
"@
