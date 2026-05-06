param(
    [switch]$IncludeDotnetRunParents
)

$ErrorActionPreference = "Stop"

$projectDlls = @(
    "TourGuide.API.dll",
    "TourGuide.WebAdmin.dll",
    "TourGuide.WebQR.dll"
)

$ports = @(7095, 7195, 7118, 5276, 5275, 5119)
$processIds = New-Object System.Collections.Generic.HashSet[int]

Get-CimInstance Win32_Process |
    Where-Object {
        $commandLine = $_.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            return $false
        }

        foreach ($dll in $projectDlls) {
            if ($commandLine -like "*$dll*") {
                return $true
            }
        }

        return $false
    } |
    ForEach-Object { [void]$processIds.Add([int]$_.ProcessId) }

$netstat = netstat -ano
foreach ($port in $ports) {
    $pattern = "[:.]$port\s+.*LISTENING\s+(\d+)"
    foreach ($line in $netstat) {
        if ($line -match $pattern) {
            [void]$processIds.Add([int]$Matches[1])
        }
    }
}

foreach ($id in $processIds) {
    try {
        $process = Get-Process -Id $id -ErrorAction Stop
        Write-Host "Stopping $($process.ProcessName) PID $id"
        Stop-Process -Id $id -Force
    }
    catch {
        Write-Host "PID $id is already stopped."
    }
}

if ($IncludeDotnetRunParents) {
    Get-CimInstance Win32_Process |
        Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*dotnet run*TourGuide*" } |
        ForEach-Object {
            Write-Host "Stopping dotnet run parent PID $($_.ProcessId)"
            Stop-Process -Id $_.ProcessId -Force
        }
}

Write-Host "TourGuide server stop complete."
