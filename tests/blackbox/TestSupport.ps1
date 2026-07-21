Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 does not preload this assembly. Loading it explicitly
# keeps the test runner compatible with both powershell.exe and pwsh.exe.
Add-Type -AssemblyName System.Net.Http

function New-GymMasterHttpClient {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [string]$AccessToken
    )

    $client = [System.Net.Http.HttpClient]::new()
    $client.BaseAddress = [Uri]::new($BaseUrl.TrimEnd('/') + '/')
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
        $client.DefaultRequestHeaders.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AccessToken)
    }
    return $client
}

function Invoke-GymMasterRequest {
    param(
        [Parameter(Mandatory = $true)][System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body
    )

    $request = $null
    $response = $null
    try {
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method.ToUpperInvariant()),
            $Path.TrimStart('/'))

        if ($null -ne $Body) {
            $jsonBody = $Body | ConvertTo-Json -Depth 20 -Compress
            $request.Content = [System.Net.Http.StringContent]::new(
                $jsonBody,
                [System.Text.Encoding]::UTF8,
                'application/json')
        }

        $timer = [System.Diagnostics.Stopwatch]::StartNew()
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        $timer.Stop()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $json = $null
        if (-not [string]::IsNullOrWhiteSpace($content)) {
            try { $json = $content | ConvertFrom-Json } catch { }
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            DurationMs = $timer.Elapsed.TotalMilliseconds
            Json = $json
            Content = $content
            Error = $null
        }
    }
    catch {
        return [pscustomobject]@{
            StatusCode = 0
            DurationMs = 0
            Json = $null
            Content = ''
            Error = $_.Exception.Message
        }
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        if ($null -ne $request) { $request.Dispose() }
    }
}

function New-GymMasterCase {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][bool]$Passed,
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$Actual,
        [string]$Detail = ''
    )

    return [pscustomobject]@{
        Id = $Id
        Name = $Name
        Result = $(if ($Passed) { 'PASS' } else { 'FAIL' })
        Expected = $Expected
        Actual = $Actual
        Detail = $Detail
    }
}

function Test-GymMasterProperty {
    param([object]$Object, [string]$Name)
    return $null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name
}

function Complete-GymMasterSuite {
    param(
        [Parameter(Mandatory = $true)][string]$Suite,
        [Parameter(Mandatory = $true)][System.Collections.IEnumerable]$Results,
        [string]$ReportPath
    )

    $all = @($Results)
    $passed = @($all | Where-Object Result -eq 'PASS').Count
    $failed = $all.Count - $passed

    Write-Host ""
    Write-Host "=== $Suite ==="
    $all | Format-Table Id, Result, Name, Expected, Actual -AutoSize | Out-Host
    Write-Host "Passed: $passed/$($all.Count); Failed: $failed"

    if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
        $fullPath = [System.IO.Path]::GetFullPath($ReportPath)
        $directory = [System.IO.Path]::GetDirectoryName($fullPath)
        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        }
        [pscustomobject]@{
            suite = $Suite
            runAtUtc = [DateTime]::UtcNow
            passed = $passed
            failed = $failed
            results = $all
        } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $fullPath -Encoding utf8
        Write-Host "Report: $fullPath"
    }

    return ($failed -eq 0)
}
