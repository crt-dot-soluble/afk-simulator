param([string]$path)

if (-not (Test-Path -Path $path)) {
    return
}

$content = Get-Content -Path $path
$count = 0
for ($i = 0; $i -lt $content.Length; $i++) {
    if ($content[$i] -match '^pick ' -and $count -lt 4) {
        $content[$i] = $content[$i] -replace '^pick', 'reword'
        $count++
    }
}
Set-Content -Path $path -Value $content
