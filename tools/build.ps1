Write-Host "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path ..\artifacts) {
	echo "build: Cleaning ..\artifacts"
	Remove-Item ..\artifacts -Force -Recurse
}

Write-Host "build: Attempting to pack file..."
	& nuget pack ..\burcin.nuspec -NonInteractive -OutputDirectory ..\artifacts -Verbosity Detailed -version $env:APPVEYOR_BUILD_VERSION
    if($LASTEXITCODE -ne 0) { exit 1 }

Pop-Location
