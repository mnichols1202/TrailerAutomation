$date = Get-Date -Format 'MMddyyyyHHmmss'
$content = "namespace TrailerAutomationClientNet { public static class BuildVersion { public const string Version = `"$date`"; } }"
$outputPath = Join-Path $PSScriptRoot "BuildVersion.cs"
Set-Content -Path $outputPath -Value $content

# Also write to shared ClientVersion.txt for Pico/S3
$sharedVersionPath = Join-Path $PSScriptRoot "..\ClientVersion.txt"
Set-Content -Path $sharedVersionPath -Value $date

Write-Host "Generated BuildVersion.cs with version: $date"
Write-Host "Generated ClientVersion.txt for Pico/S3: $date"
