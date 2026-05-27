function Get-PicoConfig {
    param([string]$Ip)
    $client = New-Object System.Net.Sockets.TcpClient
    $client.SendTimeout = 3000
    $client.ReceiveTimeout = 8000
    $client.Connect($Ip, 8888)
    $stream = $client.GetStream()
    $request = '{"commandId":"r","type":"getConfigRaw","payload":{}}' + "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush()

    $ms = New-Object System.IO.MemoryStream
    $buffer = New-Object byte[] 4096
    Start-Sleep -Milliseconds 800
    while ($stream.DataAvailable) {
        $n = $stream.Read($buffer, 0, 4096)
        if ($n -le 0) { break }
        $ms.Write($buffer, 0, $n)
        Start-Sleep -Milliseconds 150
    }
    if ($ms.Length -eq 0) {
        $n = $stream.Read($buffer, 0, 4096)
        $ms.Write($buffer, 0, $n)
    }
    $client.Close()

    $resp = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $resp | ConvertFrom-Json
    return $obj.data.configJson
}

$mapping = @{
    '192.168.1.108' = 'data-Pico-Inside.json'
    '192.168.1.219' = 'data-Pico-Outside.json'
    '192.168.1.109' = 'data-PicoW-001.json'
}
$root = 'C:\Users\MichaelNichols\source\TrailerAutomation'

foreach ($entry in $mapping.GetEnumerator()) {
    try {
        $cfg = Get-PicoConfig $entry.Key
        $path = Join-Path $root $entry.Value
        Set-Content -Path $path -Value $cfg -Encoding UTF8 -NoNewline
        $size = (Get-Item $path).Length
        Write-Output "$($entry.Key) -> $($entry.Value) ($size bytes)"
    } catch {
        Write-Output "$($entry.Key) -> ERROR: $($_.Exception.Message)"
    }
}
