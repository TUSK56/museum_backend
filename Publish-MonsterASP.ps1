# Builds a MonsterASP.net-ready folder at publish/MonsterASP.net (PostgreSQL / Railway).
param(
    [string]$OutputDir = "publish\MonsterASP.net",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "Publishing VirtualMuseum.API ($Configuration) -> $OutputDir"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "dotnet not found locally. Trying Docker SDK image..."
    docker run --rm `
        -v "${root}:/src" `
        -w /src `
        mcr.microsoft.com/dotnet/sdk:8.0 `
        dotnet publish "VirtualMuseum.API/VirtualMuseum.API.csproj" `
            -c $Configuration `
            -o "/src/$OutputDir" `
            /p:UseAppHost=false
}
else {
    dotnet publish "VirtualMuseum.API/VirtualMuseum.API.csproj" `
        -c $Configuration `
        -o $OutputDir `
        /p:UseAppHost=false
}

# Production settings for MonsterASP + Railway Postgres
Copy-Item -Force "VirtualMuseum.API\appsettings.Production.json" "$OutputDir\appsettings.Production.json"
Copy-Item -Force "VirtualMuseum.API\web.config" "$OutputDir\web.config"
if (-not (Test-Path "VirtualMuseum.API\appsettings.Production.json")) {
    Write-Warning "Missing VirtualMuseum.API\appsettings.Production.json — copy appsettings.Production.example.json and set your Railway connection string."
}
if (Test-Path "VirtualMuseum.API\appsettings.Production.json") {
    $prod = Get-Content "VirtualMuseum.API\appsettings.Production.json" -Raw | ConvertFrom-Json
    $conn = $prod.ConnectionStrings.DefaultConnection
    if ($conn) {
        $webConfigPath = Join-Path $OutputDir "web.config"
        [xml]$xml = Get-Content $webConfigPath
        $envVars = $xml.configuration.location.'system.webServer'.aspNetCore.environmentVariables
        $node = $envVars.environmentVariable | Where-Object { $_.name -eq 'ConnectionStrings__DefaultConnection' }
        if ($node) { $node.value = $conn } else {
            $newVar = $xml.CreateElement('environmentVariable')
            $newVar.SetAttribute('name', 'ConnectionStrings__DefaultConnection')
            $newVar.SetAttribute('value', $conn)
            $envVars.AppendChild($newVar) | Out-Null
        }
        $xml.Save($webConfigPath)
    }
}

# MonsterASP runs Production by default on many plans; merge Production connection into appsettings.json for hosts that ignore ASPNETCORE_ENVIRONMENT.
$appsettingsPath = Join-Path $OutputDir "appsettings.json"
$productionPath = Join-Path $OutputDir "appsettings.Production.json"
if ((Test-Path $appsettingsPath) -and (Test-Path $productionPath)) {
    $base = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    $prod = Get-Content $productionPath -Raw | ConvertFrom-Json
    if ($prod.ConnectionStrings.DefaultConnection) {
        if (-not $base.ConnectionStrings) { $base | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue (@{}) }
        $base.ConnectionStrings.DefaultConnection = $prod.ConnectionStrings.DefaultConnection
    }
    if ($prod.Database) { $base.Database = $prod.Database }
    $base | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
}

New-Item -ItemType Directory -Force -Path "$OutputDir\logs" | Out-Null
Write-Host "Done. Upload everything in '$OutputDir' to your MonsterASP site root."
