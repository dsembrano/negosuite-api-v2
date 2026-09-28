param([string]$Image = 'negosuite-api-v2:phase4')
$ErrorActionPreference = 'Stop'
$name = 'negosuite-smoke-' + [guid]::NewGuid().ToString('N')
$started = $false
$client = $null
try {
    docker run --detach --rm --name $name --publish '127.0.0.1::8080' `
        --env 'ConnectionString__negosuite=server=127.0.0.1;port=1;database=unavailable;user=test;password=test;Connection Timeout=1' `
        --env 'Jwt__Key=container-smoke-only-key-at-least-32-bytes' `
        --env 'Jwt__Issuer=container-smoke' --env 'Jwt__Audience=container-smoke' $Image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Container startup failed.' }
    $started = $true
    $ports = docker inspect --format '{{json .NetworkSettings.Ports}}' $name | ConvertFrom-Json
    $port = $ports.'8080/tcp'[0].HostPort
    $url = "http://127.0.0.1:$port"
    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object Net.Http.HttpClientHandler
    $handler.UseProxy = $false
    $handler.AllowAutoRedirect = $false
    $client = New-Object Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(3)
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        try {
            $response = $client.GetAsync("$url/api/health").GetAwaiter().GetResult()
            $ready = $response.IsSuccessStatusCode
            $response.Dispose()
            if ($ready) { break }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'Container health endpoint did not become ready.' }
    foreach ($case in @(@('/api/health', 200), @('/api/customers', 401), @('/swagger/v1/swagger.json', 404))) {
        $response = $client.GetAsync($url + $case[0]).GetAwaiter().GetResult()
        if ([int]$response.StatusCode -ne $case[1]) { throw "Unexpected status for $($case[0]): $([int]$response.StatusCode)" }
        Write-Output "$($case[0]): $([int]$response.StatusCode)"
        $response.Dispose()
    }
    $body = New-Object Net.Http.StringContent('{}', [Text.Encoding]::UTF8, 'application/json')
    $response = $client.PostAsync("$url/api/auth/refresh-access-token", $body).GetAwaiter().GetResult()
    if ([int]$response.StatusCode -ne 400) { throw 'Empty refresh request did not return 400.' }
    $response.Dispose()
    $body.Dispose()
    Write-Output '/api/auth/refresh-access-token: 400'
    $uid = docker exec $name id -u
    if ($LASTEXITCODE -ne 0 -or $uid -eq '0') { throw 'Container must run as a non-root user.' }
    docker exec $name sh -c 'test ! -e /app/appsettings.json && test ! -e /app/appsettings.Development.json && test ! -d /app/DoConfig && test ! -d /app/Db && test ! -d /app/tests'
    if ($LASTEXITCODE -ne 0) { throw 'Unexpected development/configuration files in image.' }
    Write-Output "Container checks passed; UID=$uid."
    docker exec $name dotnet --list-runtimes
} finally {
    if ($client) { $client.Dispose() }
    if ($started) { docker stop --time 10 $name | Out-Null }
}
