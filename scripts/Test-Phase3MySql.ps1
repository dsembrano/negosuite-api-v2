param([string]$Filter)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$sandbox = Join-Path $repo 'bin/phase1-mysql'
$state = Get-Content "$sandbox/state.json" -Raw | ConvertFrom-Json
if ($state.port -ne 33316 -or $state.hostAddress -ne '127.0.0.1') { throw 'Unexpected sandbox address.' }
$expectedData = [IO.Path]::GetFullPath((Join-Path $sandbox 'data'))
if ([IO.Path]::GetFullPath($state.dataDirectory) -ne $expectedData) { throw 'Unexpected sandbox data directory.' }
$listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 33316)
$listener.Start()
$listener.Stop()
$oldConnection = $env:NEGOSUITE_PHASE3_MYSQL
$daemon = $null
Push-Location $repo
try {
    $daemon = Start-Process -FilePath $state.serverExecutable -ArgumentList @('--no-defaults', ('--datadir="' + $expectedData + '"'), '--bind-address=127.0.0.1', '--port=33316', '--mysqlx=OFF', '--skip-log-bin', ('--log-error="' + "$sandbox/server.log" + '"')) -WindowStyle Hidden -PassThru
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        if ($daemon.HasExited) { throw 'Isolated MySQL failed to start.' }
        $tcp = New-Object Net.Sockets.TcpClient
        try { $tcp.Connect('127.0.0.1', 33316); $ready = $true } catch { } finally { $tcp.Dispose() }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw 'Isolated MySQL startup timeout.' }
    $passwordLine = Get-Content "$sandbox/client.cnf" | Where-Object { $_ -like 'password=*' }
    $password = $passwordLine.Substring('password='.Length)
    $env:NEGOSUITE_PHASE3_MYSQL = "server=127.0.0.1;port=33316;user=root;password=$password;SslMode=Required"
    $testArgs = @('test', 'tests/Negosuite.Api.CompatibilityTests/Negosuite.Api.CompatibilityTests.csproj', '-c', 'Release', '--no-restore', '--logger', 'trx;LogFileName=phase3-mysql.trx', '--results-directory', 'bin/phase3-verification')
    if ($Filter) { $testArgs += @('--filter', $Filter) }
    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Phase 3 tests failed. See test results.' }
} finally {
    $env:NEGOSUITE_PHASE3_MYSQL = $oldConnection
    if ($daemon -and !$daemon.HasExited) {
        & mysql "--defaults-extra-file=$sandbox/client.cnf" --execute='SHUTDOWN;'
        if (!$daemon.WaitForExit(15000)) { $daemon.Kill(); $daemon.WaitForExit() }
    }
    if ($daemon) { $daemon.Dispose() }
    Pop-Location
}
