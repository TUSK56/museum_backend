# API smoke tests for Virtual Museum API
# Prerequisites: API running (e.g. dotnet run --project VirtualMuseum.API)
# Usage: .\API-Tests.ps1
# Optional: .\API-Tests.ps1 -BaseUrl "https://egymuseum.runasp.net"

[CmdletBinding()]
param(
    [string] $BaseUrl = "https://egymuseum.runasp.net"
)

$ErrorActionPreference = "Stop"
$script:UserToken = $null
$script:AdminToken = $null

function Invoke-ApiRequest {
    param(
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string] $Method,
        [string] $Uri,
        [object] $Body = $null,
        [string] $BearerToken = $null
    )

    $headers = @{}
    if ($BearerToken) {
        $headers["Authorization"] = "Bearer $BearerToken"
    }

    $params = @{
        Uri             = $Uri
        Method          = $Method
        Headers         = $headers
        UseBasicParsing = $true
    }

    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = ($Body | ConvertTo-Json -Depth 12 -Compress)
    }

    try {
        $resp = Invoke-WebRequest @params
        $json = $null
        if ($resp.Content) {
            $json = $resp.Content | ConvertFrom-Json
        }
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            Json       = $json
            Raw        = $resp.Content
        }
    }
    catch {
        $ex = $_.Exception
        $code = 0
        $raw = $null
        $json = $null

        if ($ex.Response) {
            $code = [int]$ex.Response.StatusCode
            $stream = $ex.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $raw = $reader.ReadToEnd()
                $reader.Dispose()
                if ($raw) {
                    try { $json = $raw | ConvertFrom-Json } catch { }
                }
            }
        }

        return [pscustomobject]@{
            StatusCode = $code
            Json       = $json
            Raw        = $raw
            Error      = $ex.Message
        }
    }
}

function Write-Step {
    param([string] $Message)
    Write-Host ""
    Write-Host "=== $Message ===" -ForegroundColor Cyan
}

# --- Run tests ---

Write-Step "0. API reachable (Swagger)"
$probe = Invoke-ApiRequest -Method GET -Uri "$BaseUrl/swagger/v1/swagger.json"
if ($probe.StatusCode -ne 200) {
    Write-Host "Cannot reach $BaseUrl - HTTP $($probe.StatusCode). Start the API: dotnet run --project VirtualMuseum.API" -ForegroundColor Red
    if ($probe.Error) { Write-Host $probe.Error -ForegroundColor DarkYellow }
    exit 1
}
Write-Host "OK"

Write-Step "1. Register (unique email)"
$testEmail = ('ps1-{0}@museum.com' -f ([guid]::NewGuid().ToString('N').Substring(0, 12)))
$reg = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/auth/register" -Body @{
    fullName = "PS1 Test User"
    email    = $testEmail
    region   = "Test"
    password = "Test@123"
}
if ($reg.StatusCode -in 200, 201 -and $reg.Json.success) {
    Write-Host "OK register: $testEmail"
}
else {
    Write-Host "FAIL register: HTTP $($reg.StatusCode) $($reg.Error)" -ForegroundColor Red
    if ($reg.Raw) { Write-Host $reg.Raw }
    exit 1
}

Write-Step "2. Send OTP + verify email (expects Smtp:Enabled=false so code is returned)"
$otpResp = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/auth/send-otp" -Body @{ email = $testEmail }
$otpCode = $null
if ($otpResp.Json.data -and $otpResp.Json.data.code) {
    $otpCode = $otpResp.Json.data.code
    Write-Host "OK: OTP from response (dev mode)"
}
else {
    Write-Host "SKIP user-token tests: no OTP in response (enable Smtp:Enabled=false or verify email manually)." -ForegroundColor Yellow
}

if ($otpCode) {
    $verify = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/auth/verify-otp" -Body @{
        email = $testEmail
        code  = $otpCode
    }
    if ($verify.StatusCode -eq 200 -and $verify.Json.success) {
        Write-Host "OK: Email verified"
    }
    else {
        Write-Host "FAIL verify-otp: $($verify.StatusCode) $($verify.Raw)" -ForegroundColor Red
        $otpCode = $null
    }
}

Write-Step "3. Login (User) - after verify only"
if ($otpCode) {
    $userLogin = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/auth/login" -Body @{
        email    = $testEmail
        password = "Test@123"
    }
    if ($userLogin.Json.data.accessToken) {
        $script:UserToken = $userLogin.Json.data.accessToken
        Write-Host "OK: User accessToken received (role: $($userLogin.Json.data.role))"
    }
    else {
        Write-Host "FAIL user login: $($userLogin.StatusCode) $($userLogin.Raw)" -ForegroundColor Red
    }
}
else {
    Write-Host "SKIP"
}

Write-Step "4. Login (Admin)"
$adminLogin = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/auth/login" -Body @{
    email    = "admin@museum.com"
    password = "admin@123"
}
if ($adminLogin.Json.data.accessToken) {
    $script:AdminToken = $adminLogin.Json.data.accessToken
    Write-Host "OK: Admin accessToken received (role: $($adminLogin.Json.data.role))"
}
else {
    Write-Host "FAIL admin login: $($adminLogin.StatusCode) $($adminLogin.Raw)" -ForegroundColor Red
    exit 1
}

Write-Step "5. GET Artifacts / Categories / Eras / Materials / Tags (anonymous)"
foreach ($path in @("artifacts", "categories", "eras", "materials", "tags")) {
    $r = Invoke-ApiRequest -Method GET -Uri "$BaseUrl/api/$path"
    if ($r.StatusCode -eq 200 -and $r.Json.success) {
        $cnt = 0
        if ($r.Json.data) { $cnt = @($r.Json.data).Count }
        Write-Host ('OK GET /api/{0} ({1} items)' -f $path, $cnt)
    }
    else {
        Write-Host "FAIL GET /api/$path : $($r.StatusCode)" -ForegroundColor Red
    }
}

Write-Step "6. GET /api/users (Admin JWT)"
$users = Invoke-ApiRequest -Method GET -Uri "$BaseUrl/api/users" -BearerToken $AdminToken
if ($users.StatusCode -eq 200 -and $users.Json.success) {
    Write-Host "OK: users list"
}
else {
    Write-Host "FAIL: $($users.StatusCode)" -ForegroundColor Red
}

Write-Step "7. POST /api/eras - no token (expect 401)"
$noAuth = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/eras" -Body @{
    name      = "Should Fail"
    startYear = 1
    endYear   = 2
}
if ($noAuth.StatusCode -eq 401) {
    Write-Host "OK: 401 Unauthorized"
}
else {
    Write-Host "FAIL: expected 401, got $($noAuth.StatusCode)" -ForegroundColor Red
}

Write-Step "8. POST /api/eras - User JWT (expect 403)"
if ($UserToken) {
    $userPost = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/eras" -Body @{
        name      = "User Should Fail"
        startYear = 1
        endYear   = 2
    } -BearerToken $UserToken
    if ($userPost.StatusCode -eq 403) {
        Write-Host "OK: 403 Forbidden"
    }
    else {
        Write-Host "FAIL: expected 403, got $($userPost.StatusCode)" -ForegroundColor Red
    }
}
else {
    Write-Host "SKIP (no user token)"
}

Write-Step "9. POST /api/eras - Admin JWT (expect 201)"
$eraName = ('PS1-Era-{0}' -f ([guid]::NewGuid().ToString('N').Substring(0, 8)))
$adminPost = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/eras" -Body @{
    name      = $eraName
    startYear = -100
    endYear   = 100
} -BearerToken $AdminToken
if ($adminPost.StatusCode -eq 201 -and $adminPost.Json.success) {
    Write-Host "OK: Created era $eraName"
}
else {
    Write-Host "FAIL: $($adminPost.StatusCode) $($adminPost.Raw)" -ForegroundColor Red
}

Write-Step "10. Forgot-password request (generic 200)"
$fpReq = Invoke-ApiRequest -Method POST -Uri "$BaseUrl/api/auth/forgot-password/request" -Body @{ email = $testEmail }
if ($fpReq.StatusCode -eq 200 -and $fpReq.Json.success) {
    Write-Host "OK: forgot-password/request (reset uses OTP emailed when SMTP enabled)"
}
else {
    Write-Host "FAIL: $($fpReq.StatusCode)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Tests Complete ===" -ForegroundColor Green
Write-Host 'Note: Forgot-password reset is not automated here (OTP is not returned in API response). Use integration tests or SMTP.'

