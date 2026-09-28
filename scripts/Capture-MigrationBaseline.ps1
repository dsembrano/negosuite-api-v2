param(
    [string]$OutputDirectory = 'bin/migration-current',
    [string]$SdkPath,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
$process = $null
try {
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $buildArgs = @()
    if ($SdkPath) { $buildArgs += $SdkPath }
    $buildArgs += @('build', 'negosuite-api.csproj', '-c', 'Release', '-t:Rebuild', '-o', "$output/app")
    if ($NoRestore) { $buildArgs += '--no-restore' }
    & dotnet @buildArgs 2>&1 | Out-File "$output/build.log" -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Build failed. See $output/build.log" }

    # Reserve an available loopback port; the short release/start gap is bounded by the startup check.
    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $url = "http://127.0.0.1:$port"
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command dotnet).Source
    $start.Arguments = '"' + "$output/app/negosuite-api.dll" + '"'
    $start.WorkingDirectory = $repo
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $start.EnvironmentVariables['DOTNET_ENVIRONMENT'] = 'Development'
    $start.EnvironmentVariables['ASPNETCORE_URLS'] = $url
    $start.EnvironmentVariables['ASPNETCORE_HTTPS_PORT'] = ''
    # Restricted Windows sessions cannot write the default EventLog provider.
    $start.EnvironmentVariables['Logging__EventLog__LogLevel__Default'] = 'None'
    # Never connect to the configured application database or use its JWT secret.
    $start.EnvironmentVariables['ConnectionString__negosuite'] = 'server=127.0.0.1;port=1;database=baseline_unavailable;user=baseline;password=baseline;Connection Timeout=1'
    $start.EnvironmentVariables['Jwt__Key'] = 'baseline-only-test-signing-key-32-characters-minimum'
    $start.EnvironmentVariables['Jwt__Issuer'] = 'baseline-local'
    $start.EnvironmentVariables['Jwt__Audience'] = 'baseline-local'
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object Net.Http.HttpClientHandler
    $handler.UseProxy = $false
    $client = New-Object Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(10)
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($process.HasExited) { throw 'Baseline API exited during startup.' }
        try {
            $response = $client.GetAsync("$url/api/health").GetAwaiter().GetResult()
            $ready = $response.IsSuccessStatusCode
            $response.Dispose()
            if ($ready) { break }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw 'Baseline API did not become healthy.' }

    $cases = @(
        @{ Name = 'health'; Method = 'GET'; Path = '/api/health'; Expected = 200 },
        @{ Name = 'swagger'; Method = 'GET'; Path = '/swagger/v1/swagger.json'; Expected = 200 },
        @{ Name = 'protected-customers'; Method = 'GET'; Path = '/api/customers'; Expected = 401 },
        @{ Name = 'refresh-without-token'; Method = 'POST'; Path = '/api/auth/refresh-access-token'; Body = '{}'; Expected = 400 }
    )
    $results = foreach ($case in $cases) {
        $request = New-Object Net.Http.HttpRequestMessage([Net.Http.HttpMethod]::new($case.Method), "$url$($case.Path)")
        if ($case.Body) { $request.Content = New-Object Net.Http.StringContent($case.Body, [Text.Encoding]::UTF8, 'application/json') }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ($case.Name -eq 'swagger' -and $response.IsSuccessStatusCode) {
            [IO.File]::WriteAllText("$output/openapi.json", $body)
        }
        [pscustomobject]@{
            Name = $case.Name; Method = $case.Method; Path = $case.Path
            Status = [int]$response.StatusCode; Expected = $case.Expected
            Passed = ([int]$response.StatusCode -eq $case.Expected)
            ContentType = [string]$response.Content.Headers.ContentType
            Body = $(if ($case.Name -eq 'swagger') { '(see openapi.json when successful)' } else { $body })
        }
        $request.Dispose()
        $response.Dispose()
    }
    $results | ConvertTo-Json -Depth 10 | Out-File "$output/http-baseline.json" -Encoding utf8
    $results | Select-Object Name, Status, Expected, Passed | Format-Table
    if ($results.Passed -contains $false) { throw 'Baseline checks differ from expected statuses; inspect http-baseline.json.' }
} finally {
    if ($client) { $client.Dispose() }
    if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    if ($process) {
        if ($stdout) { $stdout.GetAwaiter().GetResult() | Out-File "$output/runtime.log" -Encoding utf8 }
        if ($stderr) { $stderr.GetAwaiter().GetResult() | Out-File "$output/runtime-errors.log" -Encoding utf8 }
        $process.Dispose()
    }
    Pop-Location
}
