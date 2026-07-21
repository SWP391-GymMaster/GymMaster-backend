param(
    [string]$BaseUrl = 'http://127.0.0.1:5042',
    [string]$Path = '/api/v1/dashboard/summary',
    [string]$AdminEmail = 'admin@gymmaster.local',
    [string]$AdminPassword = 'Admin123!',
    [ValidateRange(1, 10000)][int]$RequestCount = 200,
    [ValidateRange(1, 500)][int]$ConcurrentUsers = 50,
    [ValidateRange(1, 60000)][double]$P95LimitMs = 2000,
    [ValidateRange(0, 100)][double]$MaxErrorRatePercent = 1,
    [string]$ReportPath = ''
)

. (Join-Path $PSScriptRoot 'TestSupport.ps1')

$loginClient = New-GymMasterHttpClient -BaseUrl $BaseUrl
try {
    $login = Invoke-GymMasterRequest -Client $loginClient -Method POST -Path '/api/v1/auth/login' -Body @{
        identifier = $AdminEmail
        password = $AdminPassword
    }
}
finally {
    $loginClient.Dispose()
}

$loginOk = $login.StatusCode -eq 200 -and
    $null -ne $login.Json -and
    (Test-GymMasterProperty $login.Json 'data') -and
    $null -ne $login.Json.data -and
    (Test-GymMasterProperty $login.Json.data 'accessToken')
if (-not $loginOk) {
    Write-Error "Performance test cannot authenticate. HTTP $($login.StatusCode). $($login.Error)"
    exit 1
}

if (-not ('GymMaster.Performance.LoadProbe' -as [type])) {
    $httpAssembly = [System.Net.Http.HttpClient].Assembly.Location
    Add-Type -ReferencedAssemblies $httpAssembly -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GymMaster.Performance
{
    public sealed class ProbeResult
    {
        public double DurationMs { get; set; }
        public int StatusCode { get; set; }
        public string Error { get; set; }
    }

    public static class LoadProbe
    {
        public static async Task<ProbeResult[]> RunAsync(
            string baseUrl,
            string path,
            string accessToken,
            int requestCount,
            int concurrency)
        {
            using (var client = new HttpClient())
            using (var gate = new SemaphoreSlim(concurrency, concurrency))
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var tasks = Enumerable.Range(0, requestCount).Select(async _ =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);
                    var timer = Stopwatch.StartNew();
                    try
                    {
                        using (var response = await client.GetAsync(path.TrimStart('/')).ConfigureAwait(false))
                        {
                            timer.Stop();
                            return new ProbeResult
                            {
                                DurationMs = timer.Elapsed.TotalMilliseconds,
                                StatusCode = (int)response.StatusCode,
                                Error = null
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        timer.Stop();
                        return new ProbeResult
                        {
                            DurationMs = timer.Elapsed.TotalMilliseconds,
                            StatusCode = 0,
                            Error = ex.GetBaseException().Message
                        };
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToArray();

                return await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
    }
}
'@
}

Write-Host "Warm-up: GET $Path"
$warmup = New-GymMasterHttpClient -BaseUrl $BaseUrl -AccessToken ([string]$login.Json.data.accessToken)
try {
    1..3 | ForEach-Object { Invoke-GymMasterRequest -Client $warmup -Method GET -Path $Path | Out-Null }
}
finally {
    $warmup.Dispose()
}

Write-Host "Load: $RequestCount requests, concurrency $ConcurrentUsers"
$wallClock = [System.Diagnostics.Stopwatch]::StartNew()
$samples = [GymMaster.Performance.LoadProbe]::RunAsync(
    $BaseUrl,
    $Path,
    [string]$login.Json.data.accessToken,
    $RequestCount,
    $ConcurrentUsers).GetAwaiter().GetResult()
$wallClock.Stop()

$durations = @($samples | ForEach-Object DurationMs | Sort-Object)
function Get-Percentile([double[]]$Values, [double]$Percentile) {
    $index = [Math]::Max(0, [Math]::Ceiling($Values.Count * $Percentile) - 1)
    return $Values[$index]
}

$successCount = @($samples | Where-Object { $_.StatusCode -ge 200 -and $_.StatusCode -lt 300 }).Count
$errorCount = $samples.Count - $successCount
$errorRate = if ($samples.Count -eq 0) { 100 } else { $errorCount * 100.0 / $samples.Count }
$throughput = if ($wallClock.Elapsed.TotalSeconds -eq 0) { 0 } else { $samples.Count / $wallClock.Elapsed.TotalSeconds }
$p50 = Get-Percentile $durations 0.50
$p95 = Get-Percentile $durations 0.95
$p99 = Get-Percentile $durations 0.99
$passed = $p95 -le $P95LimitMs -and $errorRate -le $MaxErrorRatePercent

$result = [pscustomobject]@{
    suite = 'GymMaster performance baseline'
    runAtUtc = [DateTime]::UtcNow
    baseUrl = $BaseUrl
    path = $Path
    requests = $RequestCount
    concurrentUsers = $ConcurrentUsers
    wallClockMs = [Math]::Round($wallClock.Elapsed.TotalMilliseconds, 2)
    throughputRequestsPerSecond = [Math]::Round($throughput, 2)
    successCount = $successCount
    errorCount = $errorCount
    errorRatePercent = [Math]::Round($errorRate, 2)
    p50Ms = [Math]::Round($p50, 2)
    p95Ms = [Math]::Round($p95, 2)
    p99Ms = [Math]::Round($p99, 2)
    p95LimitMs = $P95LimitMs
    maxErrorRatePercent = $MaxErrorRatePercent
    result = $(if ($passed) { 'PASS' } else { 'FAIL' })
}

$result | Format-List
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $fullPath = [System.IO.Path]::GetFullPath($ReportPath)
    $directory = [System.IO.Path]::GetDirectoryName($fullPath)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fullPath -Encoding utf8
    Write-Host "Report: $fullPath"
}

if (-not $passed) { exit 1 }
