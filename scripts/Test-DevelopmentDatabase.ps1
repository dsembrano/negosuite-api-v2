param([switch]$CompareNet6)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'bin/phase5-verification'
Push-Location $repo
try {
    dotnet build tests/Negosuite.DatabaseVerification/Negosuite.DatabaseVerification.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Database verifier build failed.' }
    dotnet tests/Negosuite.DatabaseVerification/bin/Release/net10.0/Negosuite.DatabaseVerification.dll $repo
    $currentExit = $LASTEXITCODE
    if ($currentExit -notin @(0, 2)) { throw 'Verification failed before producing a readiness result.' }
    if ($CompareNet6) {
        $snapshot = Join-Path $repo 'bin/phase5-net6'
        New-Item -ItemType Directory -Force "$snapshot/api", "$snapshot/verifier" | Out-Null
        $commit = 'ba95f06c6fad0f51072a185eac234fe6f4743e54'
        git archive --format=zip "--output=$snapshot/source.zip" $commit Program.cs Startup.cs Controllers Models Services negosuite-api.csproj
        if ($LASTEXITCODE -ne 0) { throw 'Historical baseline commit is unavailable.' }
        Expand-Archive -LiteralPath "$snapshot/source.zip" -DestinationPath "$snapshot/api" -Force
        '{"sdk":{"version":"6.0.428","rollForward":"latestPatch"}}' | Set-Content "$snapshot/global.json"
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net6.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="../api/negosuite-api.csproj" /></ItemGroup></Project>' | Set-Content "$snapshot/verifier/Verifier.csproj"
        Copy-Item tests/Negosuite.DatabaseVerification/Program.cs "$snapshot/verifier/Program.cs"
        Push-Location $snapshot
        try {
            dotnet build verifier/Verifier.csproj -c Release
            if ($LASTEXITCODE -ne 0) { throw 'Historical verifier build failed; SDK/runtime 6 are required.' }
            dotnet verifier/bin/Release/net6.0/Verifier.dll $repo "$output/net6"
            if ($LASTEXITCODE -notin @(0, 2)) { throw 'Historical database verification failed.' }
        } finally { Pop-Location }
        $baseline = Get-Content "$output/net6/database-readiness.json" -Raw | ConvertFrom-Json
        $current = Get-Content "$output/database-readiness.json" -Raw | ConvertFrom-Json
        $sameSample = ($baseline.ReadOnlySample | ConvertTo-Json -Compress) -ceq ($current.ReadOnlySample | ConvertTo-Json -Compress)
        $sameResults = ($baseline.ControllerSqlComparisons | Select-Object Report,MatchesSql,Rows,Debit,Credit,NormalizedRowsSha256 | ConvertTo-Json -Depth 10 -Compress) -ceq ($current.ControllerSqlComparisons | Select-Object Report,MatchesSql,Rows,Debit,Credit,NormalizedRowsSha256 | ConvertTo-Json -Depth 10 -Compress)
        $sameSerialized = ($baseline.ControllerSqlComparisons.ResponseSha256 -join ',') -ceq ($current.ControllerSqlComparisons.ResponseSha256 -join ',')
        $comparisonsPresent = @($current.ControllerSqlComparisons).Count -eq 2 -and @($baseline.ControllerSqlComparisons).Count -eq 2 -and @($current.ControllerSqlComparisons | Where-Object { $_.MatchesSql -ne $true }).Count -eq 0 -and @($baseline.ControllerSqlComparisons | Where-Object { $_.MatchesSql -ne $true }).Count -eq 0
        [ordered]@{
            BaselineCommit = $commit; BaselineRuntime = $baseline.Runtime; CurrentRuntime = $current.Runtime
            SameSample = $sameSample; SameResults = $sameResults; RequiredComparisonsPresent = $comparisonsPresent
            SameSerializedResponses = $sameSerialized
            Passed = $sameSample -and $sameResults -and $comparisonsPresent
            Scope = 'Customer and sampled journal values/counts/totals plus independent SQL parity. Normalization sorts top-level rows by ID, sorts object keys and removes decimal trailing zero differences. Raw hashes remain in readiness reports. Not full Phase 5 acceptance.'
            Caveat = 'Sequential read-only runs against the development database; concurrent changes can cause differences. No raw records are persisted.'
        } | ConvertTo-Json | Set-Content "$output/net6-comparison.json" -Encoding utf8
        if (!$sameSample -or !$sameResults -or !$comparisonsPresent) { throw 'Baseline comparison failed or lacked the required sample; inspect reports.' }
        Write-Output 'The two read-only controller response comparisons match the .NET 6 baseline.'
    }
    if ($currentExit -eq 2) {
        Write-Warning 'Database readiness is blocked. Review database-readiness.json; no database changes were made.'
        exit 2
    }
} finally { Pop-Location }
