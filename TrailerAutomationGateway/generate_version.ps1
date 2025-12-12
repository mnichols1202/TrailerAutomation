$date = Get-Date -Format 'MMddyyyyHHmmss'
$content = "namespace TrailerAutomationGateway { public static class BuildVersion { public const string Version = `"$date`"; } }"
$outputPath = Join-Path $PSScriptRoot "BuildVersion.cs"
Set-Content -Path $outputPath -Value $content
Write-Host "Generated BuildVersion.cs with version: $date"
