Write-Host "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path ..\artifacts) {
	echo "build: Cleaning ..\artifacts"
	Remove-Item ..\artifacts -Force -Recurse
}

Write-Host "build: Version revision is $env:APPVEYOR_BUILD_NUMBER"
Write-Host "build: Version branch is $env:APPVEYOR_REPO_BRANCH"
Write-Host "build: Version version is $env:APPVEYOR_BUILD_VERSION"

Write-Host "build: Attempting to pack file..."
	& nuget pack ..\burcin.nuspec -NonInteractive -OutputDirectory ..\artifacts -Verbosity Detailed -version 1.0.473.1
    if($LASTEXITCODE -ne 0) { exit 1 }

Pop-Location
