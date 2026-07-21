param(
    [string]$BaseUrl = 'http://127.0.0.1:5042',
    [string]$AdminEmail = 'admin@gymmaster.local',
    [string]$AdminPassword = 'Admin123!',
    [string]$MemberEmail = 'member@gymmaster.local',
    [string]$MemberPassword = 'Member123!',
    [string]$ReportPath = ''
)

. (Join-Path $PSScriptRoot 'TestSupport.ps1')

$results = [System.Collections.Generic.List[object]]::new()
$publicClient = New-GymMasterHttpClient -BaseUrl $BaseUrl
$adminClient = $null
$memberClient = $null
try {
    $unauthorized = Invoke-GymMasterRequest -Client $publicClient -Method GET -Path '/api/v1/audit-logs'
    $results.Add((New-GymMasterCase 'API-001' 'Audit requires authentication' ($unauthorized.StatusCode -eq 401) 'HTTP 401' "HTTP $($unauthorized.StatusCode)" $unauthorized.Error))

    $unknownLogin = Invoke-GymMasterRequest -Client $publicClient -Method POST -Path '/api/v1/auth/login' -Body @{
        identifier = 'nobody-blackbox@gymmaster.invalid'
        password = 'NotARealPassword123!'
    }
    $unknownOk = $unknownLogin.StatusCode -eq 401 -and $null -ne $unknownLogin.Json -and $unknownLogin.Json.success -eq $false
    $results.Add((New-GymMasterCase 'API-002' 'Unknown login is rejected' $unknownOk 'HTTP 401, success=false' "HTTP $($unknownLogin.StatusCode)" $unknownLogin.Error))

    $adminLogin = Invoke-GymMasterRequest -Client $publicClient -Method POST -Path '/api/v1/auth/login' -Body @{
        identifier = $AdminEmail
        password = $AdminPassword
    }
    $adminLoginOk = $adminLogin.StatusCode -eq 200 -and $null -ne $adminLogin.Json -and
        $null -ne $adminLogin.Json.data -and (Test-GymMasterProperty $adminLogin.Json.data 'accessToken')
    $results.Add((New-GymMasterCase 'API-003' 'Admin login contract' $adminLoginOk 'HTTP 200 with data.accessToken' "HTTP $($adminLogin.StatusCode)" $adminLogin.Error))

    if ($adminLoginOk) {
        $adminClient = New-GymMasterHttpClient -BaseUrl $BaseUrl -AccessToken ([string]$adminLogin.Json.data.accessToken)

        $summary = Invoke-GymMasterRequest -Client $adminClient -Method GET -Path '/api/v1/dashboard/summary'
        $summaryShape = $summary.StatusCode -eq 200 -and $null -ne $summary.Json -and
            $summary.Json.success -eq $true -and $null -ne $summary.Json.data -and
            (Test-GymMasterProperty $summary.Json.data 'revenue') -and
            (Test-GymMasterProperty $summary.Json.data 'activeCount') -and
            (Test-GymMasterProperty $summary.Json.data 'revenueByMonth')
        $results.Add((New-GymMasterCase 'API-004' 'Dashboard summary response contract' $summaryShape 'HTTP 200 with required metrics' "HTTP $($summary.StatusCode)" $summary.Error))

        $badRange = Invoke-GymMasterRequest -Client $adminClient -Method GET -Path '/api/v1/dashboard/summary?from=2030-01-02T00%3A00%3A00Z&to=2030-01-01T00%3A00%3A00Z'
        $badRangeOk = $badRange.StatusCode -eq 422 -and $null -ne $badRange.Json -and
            $badRange.Json.success -eq $false -and $null -ne $badRange.Json.error -and
            $badRange.Json.error.code -eq 'INVALID_RANGE'
        $results.Add((New-GymMasterCase 'API-005' 'Dashboard invalid date partition' $badRangeOk 'HTTP 422, INVALID_RANGE' "HTTP $($badRange.StatusCode)" $badRange.Error))

        $audit = Invoke-GymMasterRequest -Client $adminClient -Method GET -Path '/api/v1/audit-logs?page=1&pageSize=5&search=LOGIN'
        $auditShape = $audit.StatusCode -eq 200 -and $null -ne $audit.Json -and
            $audit.Json.success -eq $true -and $null -ne $audit.Json.data -and
            (Test-GymMasterProperty $audit.Json.data 'items') -and
            (Test-GymMasterProperty $audit.Json.data 'page') -and
            (Test-GymMasterProperty $audit.Json.data 'pageSize') -and
            $audit.Json.data.pageSize -eq 5
        $results.Add((New-GymMasterCase 'API-006' 'Audit filter and pagination contract' $auditShape 'HTTP 200, pageSize=5' "HTTP $($audit.StatusCode)" $audit.Error))
    }
    else {
        foreach ($case in @(
            @('API-004', 'Dashboard summary response contract'),
            @('API-005', 'Dashboard invalid date partition'),
            @('API-006', 'Audit filter and pagination contract')
        )) {
            $results.Add((New-GymMasterCase $case[0] $case[1] $false 'Admin authorization required' 'Skipped: admin login failed'))
        }
    }

    $memberLogin = Invoke-GymMasterRequest -Client $publicClient -Method POST -Path '/api/v1/auth/login' -Body @{
        identifier = $MemberEmail
        password = $MemberPassword
    }
    $memberLoginOk = $memberLogin.StatusCode -eq 200 -and $null -ne $memberLogin.Json -and
        $null -ne $memberLogin.Json.data -and (Test-GymMasterProperty $memberLogin.Json.data 'accessToken')
    $results.Add((New-GymMasterCase 'API-007' 'Member login contract' $memberLoginOk 'HTTP 200 with data.accessToken' "HTTP $($memberLogin.StatusCode)" $memberLogin.Error))

    if ($memberLoginOk) {
        $memberClient = New-GymMasterHttpClient -BaseUrl $BaseUrl -AccessToken ([string]$memberLogin.Json.data.accessToken)

        $forbiddenDashboard = Invoke-GymMasterRequest -Client $memberClient -Method GET -Path '/api/v1/dashboard/summary'
        $results.Add((New-GymMasterCase 'API-008' 'Member cannot access admin dashboard' ($forbiddenDashboard.StatusCode -eq 403) 'HTTP 403' "HTTP $($forbiddenDashboard.StatusCode)" $forbiddenDashboard.Error))

        $memberProfile = Invoke-GymMasterRequest -Client $memberClient -Method GET -Path '/api/v1/members/me'
        $memberProfileOk = $memberProfile.StatusCode -eq 200 -and $null -ne $memberProfile.Json -and
            $null -ne $memberProfile.Json.data -and (Test-GymMasterProperty $memberProfile.Json.data 'id')
        $results.Add((New-GymMasterCase 'API-009' 'Member profile contract' $memberProfileOk 'HTTP 200 with member id' "HTTP $($memberProfile.StatusCode)" $memberProfile.Error))

        $foodItems = Invoke-GymMasterRequest -Client $memberClient -Method GET -Path '/api/v1/food-items?query=&page=1&pageSize=5'
        $foodItemsOk = $foodItems.StatusCode -eq 200 -and $null -ne $foodItems.Json -and $foodItems.Json.success -eq $true
        $results.Add((New-GymMasterCase 'API-010' 'Food item search contract' $foodItemsOk 'HTTP 200, success=true' "HTTP $($foodItems.StatusCode)" $foodItems.Error))

        if ($memberProfileOk) {
            $memberId = [long]$memberProfile.Json.data.id
            $date = Get-Date -Format 'yyyy-MM-dd'
            $mealLogs = Invoke-GymMasterRequest -Client $memberClient -Method GET -Path "/api/v1/meal-logs?memberId=$memberId&date=$date"
            $mealLogsOk = $mealLogs.StatusCode -eq 200 -and $null -ne $mealLogs.Json -and $mealLogs.Json.success -eq $true
            $results.Add((New-GymMasterCase 'API-011' 'Meal journal read contract' $mealLogsOk 'HTTP 200, success=true' "HTTP $($mealLogs.StatusCode)" $mealLogs.Error))

            $calories = Invoke-GymMasterRequest -Client $memberClient -Method GET -Path "/api/v1/members/$memberId/calorie-summary?date=$date"
            $caloriesOk = $calories.StatusCode -eq 200 -and $null -ne $calories.Json -and
                $calories.Json.success -eq $true -and $null -ne $calories.Json.data -and
                (Test-GymMasterProperty $calories.Json.data 'consumed')
            $results.Add((New-GymMasterCase 'API-012' 'Calorie summary response contract' $caloriesOk 'HTTP 200 with consumed value' "HTTP $($calories.StatusCode)" $calories.Error))
        }
        else {
            $results.Add((New-GymMasterCase 'API-011' 'Meal journal read contract' $false 'Member profile required' 'Skipped: member profile failed'))
            $results.Add((New-GymMasterCase 'API-012' 'Calorie summary response contract' $false 'Member profile required' 'Skipped: member profile failed'))
        }
    }
    else {
        foreach ($case in @(
            @('API-008', 'Member cannot access admin dashboard'),
            @('API-009', 'Member profile contract'),
            @('API-010', 'Food item search contract'),
            @('API-011', 'Meal journal read contract'),
            @('API-012', 'Calorie summary response contract')
        )) {
            $results.Add((New-GymMasterCase $case[0] $case[1] $false 'Member authorization required' 'Skipped: member login failed'))
        }
    }
}
finally {
    if ($null -ne $memberClient) { $memberClient.Dispose() }
    if ($null -ne $adminClient) { $adminClient.Dispose() }
    $publicClient.Dispose()
}

$ok = Complete-GymMasterSuite -Suite 'GymMaster API black-box tests' -Results $results -ReportPath $ReportPath
if (-not $ok) { exit 1 }
