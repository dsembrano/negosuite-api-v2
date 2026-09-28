param(
    [string]$Baseline = 'docs/migration-baseline/openapi.json',
    [string]$Candidate = 'bin/phase2-verification/openapi.json',
    [string]$OutputPath = 'bin/phase2-verification/contract-comparison.json'
)
$ErrorActionPreference = 'Stop'
function Get-Operations($spec) {
    foreach ($path in $spec.paths.PSObject.Properties) {
        foreach ($operation in $path.Value.PSObject.Properties) {
            if ($operation.Name -in @('get', 'put', 'post', 'delete', 'patch', 'head', 'options')) {
                $operation.Name.ToUpperInvariant() + ' ' + $path.Name
            }
        }
    }
}
function Add-Leaves($value, [string]$path, [hashtable]$leaves) {
    if ($value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $value.PSObject.Properties) {
            Add-Leaves $property.Value ($path + '/' + $property.Name.Replace('~', '~0').Replace('/', '~1')) $leaves
        }
    } elseif ($value -is [array]) {
        for ($i = 0; $i -lt $value.Count; $i++) { Add-Leaves $value[$i] "$path/$i" $leaves }
    } else {
        $leaves[$path] = ConvertTo-Json -InputObject $value -Compress
    }
}
$before = Get-Content $Baseline -Raw | ConvertFrom-Json
$after = Get-Content $Candidate -Raw | ConvertFrom-Json
$beforeOperations = @(Get-Operations $before)
$afterOperations = @(Get-Operations $after)
$oldLeaves = @{}
$newLeaves = @{}
Add-Leaves $before '' $oldLeaves
Add-Leaves $after '' $newLeaves
$differences = @(foreach ($path in (@($oldLeaves.Keys) + @($newLeaves.Keys) | Sort-Object -Unique)) {
    if (!$oldLeaves.ContainsKey($path) -or !$newLeaves.ContainsKey($path) -or $oldLeaves[$path] -cne $newLeaves[$path]) {
        [pscustomobject]@{ Path = $path; Before = $oldLeaves[$path]; After = $newLeaves[$path] }
    }
})
$result = [ordered]@{
    BaselineOperations = $beforeOperations.Count
    CandidateOperations = $afterOperations.Count
    AddedOperations = @($afterOperations | Where-Object { $_ -cnotin $beforeOperations })
    RemovedOperations = @($beforeOperations | Where-Object { $_ -cnotin $afterOperations })
    BaselineSchemas = @($before.components.schemas.PSObject.Properties).Count
    CandidateSchemas = @($after.components.schemas.PSObject.Properties).Count
    LeafDifferences = $differences
    Note = 'Metadata comparison only; empty containers, property order and actual database response behavior are not compared.'
}
$result | ConvertTo-Json -Depth 10 | Out-File $OutputPath -Encoding utf8
Write-Output "Operations: $($beforeOperations.Count) -> $($afterOperations.Count); metadata leaf differences: $($differences.Count). See $OutputPath."
if ($result.AddedOperations.Count -or $result.RemovedOperations.Count) { throw 'API route/method inventory changed.' }
