param(
    [Parameter(Mandatory = $true)][string]$Image,
    [Parameter(Mandatory = $true)][string]$Namespace,
    [string]$OutputPath = 'bin/phase4-verification/deployment.yml'
)
$ErrorActionPreference = 'Stop'
if ($Namespace -notmatch '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$' -or $Namespace.Length -gt 63) {
    throw 'Provide a valid explicit Kubernetes namespace.'
}
if ($Namespace -eq 'default') { throw 'Use a dedicated V2 namespace, not the default namespace.' }
if ($Image -notmatch '^[a-zA-Z0-9][a-zA-Z0-9./:_-]+@sha256:[a-f0-9]{64}$') {
    throw 'Provide an immutable registry image reference ending in @sha256:<64 lowercase hex characters>.'
}
$repo = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $repo 'DoConfig/negosuite-deployment.yml') -Raw
$manifest = $manifest.Replace('<IMAGE>', $Image)
$manifest = [regex]::Replace($manifest, '(?m)^metadata:\r?$', "metadata:`n  namespace: $Namespace")
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputPath))
New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
[IO.File]::WriteAllText($output, $manifest, [Text.UTF8Encoding]::new($false))
Write-Output "Prepared $output. No cluster operation performed."
