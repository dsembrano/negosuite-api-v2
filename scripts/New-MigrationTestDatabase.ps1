param([int]$Port = 33316)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$sandbox = Join-Path $repo 'bin/phase1-mysql'
$data = Join-Path $sandbox 'data'
if (Test-Path $sandbox) { throw "Sandbox already exists at $sandbox. It will not be overwritten." }
$listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, $Port)
$listener.Start()
$listener.Stop()
$server = (Get-Command mysqld).Source
$client = (Get-Command mysql).Source
New-Item -ItemType Directory -Path $data -Force | Out-Null
$daemon = $null
try {
    # --no-defaults prevents use of the installed service's configuration/data directory.
    $init = Start-Process -FilePath $server -ArgumentList @('--no-defaults', '--initialize-insecure', ('--datadir="' + $data + '"'), ('--log-error="' + "$sandbox/initialize.log" + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($init.ExitCode -ne 0) { throw 'MySQL initialization failed; inspect bin/phase1-mysql/initialize.log.' }
    $daemon = Start-Process -FilePath $server -ArgumentList @('--no-defaults', ('--datadir="' + $data + '"'), '--bind-address=127.0.0.1', "--port=$Port", '--mysqlx=OFF', '--skip-log-bin', ('--log-error="' + "$sandbox/server.log" + '"')) -PassThru -WindowStyle Hidden
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($daemon.HasExited) { throw 'Isolated MySQL exited; inspect bin/phase1-mysql/server.log.' }
        $tcp = New-Object Net.Sockets.TcpClient
        try { $tcp.Connect('127.0.0.1', $Port); $ready = $true } catch { } finally { $tcp.Dispose() }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw 'Isolated MySQL did not start.' }
    $password = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $client
    $start.Arguments = "--no-defaults --protocol=TCP --host=127.0.0.1 --port=$Port --user=root --connect-timeout=5 --batch"
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.WriteLine("CREATE DATABASE negosuite_baseline CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;")
    $process.StandardInput.WriteLine("ALTER USER 'root'@'localhost' IDENTIFIED BY '$password';")
    $process.StandardInput.WriteLine('SHUTDOWN;')
    $process.StandardInput.Close()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Isolated database creation failed.' }
    $process.Dispose()
    if (!$daemon.WaitForExit(15000)) { throw 'Isolated MySQL did not stop cleanly.' }
    $options = @('[client]', 'protocol=TCP', 'host=127.0.0.1', "port=$Port", 'user=root', "password=$password", 'database=negosuite_baseline')
    [IO.File]::WriteAllLines("$sandbox/client.cnf", $options, [Text.UTF8Encoding]::new($false))
    @{
        database = 'negosuite_baseline'; port = $Port; hostAddress = '127.0.0.1'
        state = 'initialized, stopped, awaiting current sanitized schema/routines/data'
        serverExecutable = $server; dataDirectory = $data
    } | ConvertTo-Json | Out-File "$sandbox/state.json" -Encoding utf8
    Write-Output "Isolated database initialized and stopped at $sandbox. Schema, routines and fixtures are still required."
} finally {
    if ($daemon -and !$daemon.HasExited) { $daemon.Kill(); $daemon.WaitForExit() }
    if ($daemon) { $daemon.Dispose() }
}
