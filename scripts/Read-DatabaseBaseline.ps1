param(
    [string]$OutputDirectory = 'bin/phase1-baseline',
    [switch]$ExportSchema,
    [ValidateSet('Complete', 'TablesOnly')]
    [string]$ExportScope = 'Complete'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # Existing settings contain a single active negosuite connection string.
    # Environment overrides take precedence, as they do in ASP.NET configuration.
    $connectionText = [Environment]::GetEnvironmentVariable('ConnectionString__negosuite')
    if (!$connectionText) {
        $raw = Get-Content appsettings.json -Raw
        $match = [regex]::Match($raw, '"negosuite"\s*:\s*("(?:\\.|[^"\\])*")')
        if (!$match.Success) { throw 'Connection string was not found.' }
        $connectionText = [string]($match.Groups[1].Value | ConvertFrom-Json)
    }
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection.set_ConnectionString($connectionText)
    $clientConfig = Join-Path $output ('mysql-client-' + [guid]::NewGuid().ToString('N') + '.cnf')
    $lines = @('[client]')
    foreach ($key in $connection.Keys) {
        $option = switch -Regex ($key) {
            '^(server|host|data source)$' { 'host' }
            '^(user|uid|user id|username)$' { 'user' }
            '^(password|pwd)$' { 'password' }
            '^(database|initial catalog)$' { 'database' }
            '^port$' { 'port' }
        }
        if ($option) {
            $value = ([string]$connection[$key]).Replace('\', '\\').Replace('"', '\"')
            $lines += $option + '="' + $value + '"'
        }
    }
    [IO.File]::WriteAllLines($clientConfig, $lines, [Text.UTF8Encoding]::new($false))
    $query = @'
SELECT 'version', VERSION();
SELECT 'table_count', COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE();
SELECT 'routine_count', COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE();
SELECT 'table', TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME;
SELECT 'routine', ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE() ORDER BY ROUTINE_NAME;
'@
    # Capture stderr privately; do not expose connection identities in the report.
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command mysql).Source
    $start.Arguments = '"--defaults-extra-file=' + $clientConfig + '" --connect-timeout=5 --batch --skip-column-names'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.StandardInput.WriteLine($query)
    $process.StandardInput.Close()
    if (!$process.WaitForExit(15000)) { $process.Kill(); throw 'Database metadata query timed out.' }
    if ($process.ExitCode -ne 0) {
        $code = [regex]::Match($stderr.GetAwaiter().GetResult(), 'ERROR\s+(\d+)').Groups[1].Value
        throw "Database metadata query failed (MySQL error $code); credentials and host suppressed."
    }
    $stdout.GetAwaiter().GetResult() | Out-File "$output/database-metadata.tsv" -Encoding utf8
    Write-Output "Database metadata saved to $output/database-metadata.tsv. No database changes made."
    $process.Dispose()
    $process = $null
    if ($ExportSchema) {
        $databaseKey = @($connection.Keys | Where-Object { $_ -match '^(database|initial catalog)$' })[0]
        $database = [string]$connection[$databaseKey]
        if ($database -notmatch '^[A-Za-z0-9_]+$') { throw 'Schema export requires a simple database identifier.' }
        # mysqldump receives the schema as a positional argument, not a client option.
        $dumpLines = @($lines | Where-Object { $_ -notmatch '^database=' })
        [IO.File]::WriteAllLines($clientConfig, $dumpLines, [Text.UTF8Encoding]::new($false))
        $dumpStart = New-Object Diagnostics.ProcessStartInfo
        $dumpStart.FileName = (Get-Command mysqldump).Source
        $objectOptions = if ($ExportScope -eq 'Complete') { '--routines --events --triggers' } else { '--skip-triggers' }
        $exportPath = Join-Path $output ("schema.$ExportScope.raw.sql")
        $dumpStart.Arguments = '"--defaults-extra-file=' + $clientConfig + '" --no-data ' + $objectOptions + ' --no-tablespaces --skip-lock-tables --set-gtid-purged=OFF --skip-dump-date --result-file="' + $exportPath + '" ' + $database
        $dumpStart.UseShellExecute = $false
        $dumpStart.CreateNoWindow = $true
        $dumpStart.RedirectStandardOutput = $true
        $dumpStart.RedirectStandardError = $true
        $process = [Diagnostics.Process]::Start($dumpStart)
        $dumpOut = $process.StandardOutput.ReadToEndAsync()
        $dumpError = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Schema export timed out; discard any partial output.' }
        if ($process.ExitCode -ne 0) {
            $safeError = $dumpError.GetAwaiter().GetResult()
            foreach ($key in $connection.Keys) {
                $value = [string]$connection[$key]
                if ($value.Length -ge 3) { $safeError = $safeError.Replace($value, '[redacted]') }
            }
            throw "Schema export failed; discard any partial output. $safeError"
        }
        Write-Output "Schema-only export ($ExportScope) saved to $exportPath. Review before committing; database definitions may contain sensitive literals or DEFINER identities."
    }
} finally {
    if ($process) { $process.Dispose() }
    if ($clientConfig -and (Test-Path -LiteralPath $clientConfig)) { Remove-Item -LiteralPath $clientConfig }
    Pop-Location
}
