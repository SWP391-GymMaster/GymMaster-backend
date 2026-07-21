param(
    [string]$BaseUrl = 'http://127.0.0.1:5042',
    [string]$AdminEmail = 'admin@gymmaster.local',
    [string]$AdminPassword = 'Admin123!',
    [string]$ReportPath = ''
)

. (Join-Path $PSScriptRoot 'TestSupport.ps1')

$results = [System.Collections.Generic.List[object]]::new()
$client = New-GymMasterHttpClient -BaseUrl $BaseUrl
try {
    # Connection layer 1: the process is reachable and the HTTP pipeline is alive.
    $health = Invoke-GymMasterRequest -Client $client -Method GET -Path '/'
    $healthOk = $health.StatusCode -eq 200 -and
        (Test-GymMasterProperty $health.Json 'status') -and $health.Json.status -eq 'running'
    $results.Add((New-GymMasterCase 'CONN-01' 'API process connection' $healthOk 'HTTP 200, status=running' "HTTP $($health.StatusCode)" $health.Error))

    # Connection layer 2: login forces a real SQL query and proves schema compatibility.
    $login = Invoke-GymMasterRequest -Client $client -Method POST -Path '/api/v1/auth/login' -Body @{
        identifier = $AdminEmail
        password = $AdminPassword
    }
    $loginOk = $login.StatusCode -eq 200 -and
        $null -ne $login.Json -and
        (Test-GymMasterProperty $login.Json 'data') -and
        $null -ne $login.Json.data -and
        (Test-GymMasterProperty $login.Json.data 'accessToken')
    $results.Add((New-GymMasterCase 'CONN-02' 'SQL Server and auth query' $loginOk 'HTTP 200 with access token' "HTTP $($login.StatusCode)" $login.Error))

    $token = if ($loginOk) { [string]$login.Json.data.accessToken } else { '' }
    if ($loginOk) {
        $authorized = New-GymMasterHttpClient -BaseUrl $BaseUrl -AccessToken $token
        try {
            $me = Invoke-GymMasterRequest -Client $authorized -Method GET -Path '/api/v1/auth/me'
            $meOk = $me.StatusCode -eq 200 -and $null -ne $me.Json -and $me.Json.success -eq $true
            $results.Add((New-GymMasterCase 'CONN-03' 'JWT and user-table read' $meOk 'HTTP 200, success=true' "HTTP $($me.StatusCode)" $me.Error))

            $dashboard = Invoke-GymMasterRequest -Client $authorized -Method GET -Path '/api/v1/dashboard/summary'
            $dashboardOk = $dashboard.StatusCode -eq 200 -and $null -ne $dashboard.Json -and $dashboard.Json.success -eq $true
            $results.Add((New-GymMasterCase 'CONN-04' 'Dashboard database queries' $dashboardOk 'HTTP 200, success=true' "HTTP $($dashboard.StatusCode)" $dashboard.Error))
        }
        finally {
            $authorized.Dispose()
        }
    }
    else {
        $results.Add((New-GymMasterCase 'CONN-03' 'JWT and user-table read' $false 'HTTP 200, success=true' 'Skipped: login failed'))
        $results.Add((New-GymMasterCase 'CONN-04' 'Dashboard database queries' $false 'HTTP 200, success=true' 'Skipped: login failed'))
    }
}
finally {
    $client.Dispose()
}

$ok = Complete-GymMasterSuite -Suite 'GymMaster connection tests' -Results $results -ReportPath $ReportPath
if (-not $ok) { exit 1 }
