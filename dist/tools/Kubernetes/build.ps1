$k8sDir = "./"
$outDir = Join-Path (Get-Location) "../../artifacts/_snapshots/output"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$repoDir = Join-Path $k8sDir "repo"
$overlaysDir = Join-Path $k8sDir "overlays"
$environments = @('production', 'staging', 'testing', 'integration')

# Save originals for reset
$repoKustomization = Join-Path $repoDir "kustomization.yaml"
$repoOriginal = Get-Content $repoKustomization -Raw

$componentOriginals = @{}

# Set image tag in repo layer
Push-Location $repoDir
kustomize edit set image "app-image:latest=ghcr.io/myowner/myrepo:snapshot"
Pop-Location

# Set namespace and build each component overlay
$namespace = "mynamespace"
foreach ($environment in $environments) {
  $envDir = Join-Path $overlaysDir $environment
  if (-not (Test-Path $envDir -PathType Container)) {
    Write-Warning "Skipping environment '$environment' because overlay directory '$envDir' does not exist."
    continue
  }

  $envKustomization = Join-Path $envDir "kustomization.yaml"
  if (-not (Test-Path $envKustomization -PathType Leaf)) {
    Write-Warning "Skipping environment '$environment' because '$envKustomization' does not exist."
    continue
  }

  $components = (Get-Content $envKustomization | Select-String '^\s*-\s+(?!.*://)(.+)' | ForEach-Object { $_.Matches.Groups[1].Value.Trim() }) | Where-Object { $_ -ne 'base' }
  foreach ($componentName in $components) {
    $componentDir = Join-Path $envDir $componentName
    $kustomizationFile = Join-Path $componentDir "kustomization.yaml"

    # Save original
    $componentOriginals[$kustomizationFile] = Get-Content $kustomizationFile -Raw

    # Set namespace
    Push-Location $componentDir
    kustomize edit set namespace "$namespace-$environment"
    Pop-Location

    # Build snapshot
    $outFile = Join-Path $outDir "${environment}_${componentName}.yaml"
    kustomize build $componentDir | Out-File $outFile -Encoding utf8
  }
}

# Reset all files to their original content
Set-Content $repoKustomization $repoOriginal -NoNewline
foreach ($file in $componentOriginals.Keys) {
  Set-Content $file $componentOriginals[$file] -NoNewline
}
Write-Output "After snapshots saved to $outDir"
