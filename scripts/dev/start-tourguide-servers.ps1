param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repo = Resolve-Path (Join-Path $PSScriptRoot "..\..")

& (Join-Path $PSScriptRoot "stop-tourguide-servers.ps1")

$apps = @(
    @{
        Name = "API"
        Project = Join-Path $repo "TourGuide.API\TourGuide.API.csproj"
        Directory = Join-Path $repo "TourGuide.API"
        Https = "https://localhost:7095"
        Http = "http://localhost:5276"
    },
    @{
        Name = "WebAdmin"
        Project = Join-Path $repo "TourGuide.WebAdmin\TourGuide.WebAdmin.csproj"
        Directory = Join-Path $repo "TourGuide.WebAdmin"
        Https = "https://localhost:7195"
        Http = "http://localhost:5275"
    },
    @{
        Name = "WebQR"
        Project = Join-Path $repo "TourGuide.WebQR\TourGuide.WebQR.csproj"
        Directory = Join-Path $repo "TourGuide.WebQR"
        Https = "https://localhost:7118"
        Http = "http://localhost:5119"
    }
)

foreach ($app in $apps) {
    $arguments = @("run", "--project", $app.Project, "--launch-profile", "https")
    if ($NoBuild) {
        $arguments += "--no-build"
    }

    Write-Host "Starting TourGuide.$($app.Name): $($app.Https)"
    Start-Process -FilePath "dotnet" -ArgumentList $arguments -WorkingDirectory $app.Directory -WindowStyle Hidden
}

Write-Host "Servers are starting. Expected URLs:"
foreach ($app in $apps) {
    Write-Host " - $($app.Name): $($app.Https)"
}
