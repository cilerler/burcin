echo "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path ..\artifacts) {
	echo "build: Cleaning ..\artifacts"
	Remove-Item ..\artifacts -Force -Recurse
}

$branch = @{ $true = $env:APPVEYOR_REPO_BRANCH; $false = $(git symbolic-ref --short -q HEAD) }[$env:APPVEYOR_REPO_BRANCH -ne $NULL];
$revision = @{ $true = "{0:00000}" -f [convert]::ToInt32("0" + $env:APPVEYOR_BUILD_NUMBER, 10); $false = "local" }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$suffix = @{ $true = ""; $false = "$($branch.Substring(0, [math]::Min(10,$branch.Length)))-$revision"}[$branch -eq "master" -and $revision -ne "local"]
$version = @{ $true = $env:APPVEYOR_BUILD_VERSION; $false = ""}

echo "build: Version suffix is $suffix"
echo "build: Version revision is $revision"
echo "build: Version branch is $branch"
echo "build: Version version is $version"

echo "Attempting to pack file: burcin.nuspec"
	& nuget pack ..\burcin.nuspec -NonInteractive -OutputDirectory ..\artifacts -Verbosity Detailed -version 1.0.473.1
    if($LASTEXITCODE -ne 0) { exit 1 }

Pop-Location
