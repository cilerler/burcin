[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-NativeCommand {
	param(
		[Parameter(Mandatory)]
		[string] $FilePath,
		[Parameter()]
		[string[]] $ArgumentList = @()
	)

	& $FilePath @ArgumentList
	if ($LASTEXITCODE -ne 0) {
		$commandText = (@($FilePath) + $ArgumentList) -join " "
		throw "'$commandText' failed with exit code $LASTEXITCODE."
	}
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$appHostVolumeRoot = Join-Path $HOME ".docker/volumes/mySolution"
$persistentDirectories = @(
	(Join-Path $HOME ".docker/volumes"),
	(Join-Path $HOME ".microsoft/usersecrets"),
	(Join-Path $HOME ".dotnet/corefx/cryptography/x509stores")
)

$userName = (Invoke-NativeCommand -FilePath "id" -ArgumentList @("-un") | Out-String).Trim()
$groupName = (Invoke-NativeCommand -FilePath "id" -ArgumentList @("-gn") | Out-String).Trim()
$ownership = "${userName}:${groupName}"

foreach ($directory in $persistentDirectories) {
	Invoke-NativeCommand -FilePath "sudo" -ArgumentList @("mkdir", "-p", "--", $directory)
	Invoke-NativeCommand -FilePath "sudo" -ArgumentList @("chown", "-R", "--", $ownership, $directory)
}

$appHostDirectories = @(
	(Join-Path $appHostVolumeRoot "mssql"),
	(Join-Path $appHostVolumeRoot "rabbitmq/mnesia"),
	(Join-Path $appHostVolumeRoot "rabbitmq-plugins"),
	(Join-Path $appHostVolumeRoot "redis/data"),
	(Join-Path $appHostVolumeRoot "redis-insight/data")
)

foreach ($directory in $appHostDirectories) {
	New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$rabbitMqPluginVersion = "4.2.0"
$rabbitMqPluginName = "rabbitmq_delayed_message_exchange-$rabbitMqPluginVersion.ez"
$rabbitMqPluginPath = Join-Path $appHostVolumeRoot "rabbitmq-plugins/$rabbitMqPluginName"
$rabbitMqPluginUri = "https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v$rabbitMqPluginVersion/$rabbitMqPluginName"
$rabbitMqPluginSha256 = "F168B2C09810CDE3726961D31F38E3408E6A7FBFF3929908D6F962061D8E70A1"

$pluginIsValid = (Test-Path -LiteralPath $rabbitMqPluginPath -PathType Leaf) -and
	((Get-FileHash -LiteralPath $rabbitMqPluginPath -Algorithm SHA256).Hash -eq $rabbitMqPluginSha256)

if (-not $pluginIsValid) {
	$downloadPath = "$rabbitMqPluginPath.download"
	try {
		Invoke-WebRequest -Uri $rabbitMqPluginUri -OutFile $downloadPath
		$downloadHash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
		if ($downloadHash -ne $rabbitMqPluginSha256) {
			throw "RabbitMQ delayed-message plugin checksum mismatch. Expected $rabbitMqPluginSha256; received $downloadHash."
		}

		Move-Item -LiteralPath $downloadPath -Destination $rabbitMqPluginPath -Force
	}
	finally {
		if (Test-Path -LiteralPath $downloadPath) {
			Remove-Item -LiteralPath $downloadPath -Force
		}
	}
}

Push-Location $repositoryRoot
try {
	Invoke-NativeCommand -FilePath "aspire" -ArgumentList @("restore", "--non-interactive")

	if (Test-Path -LiteralPath ".config/dotnet-tools.json" -PathType Leaf) {
		Invoke-NativeCommand -FilePath "dotnet" -ArgumentList @("tool", "restore")
	}
}
finally {
	Pop-Location
}
