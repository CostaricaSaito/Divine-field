param([string[]]$Paths)
$utf8Bom = [System.Text.UTF8Encoding]::new($true)
foreach ($p in $Paths) {
    $text = [System.IO.File]::ReadAllText($p)
    [System.IO.File]::WriteAllText($p, $text, $utf8Bom)
    $b = [System.IO.File]::ReadAllBytes($p)
    Write-Host ("{0}: {1:X2} {2:X2} {3:X2}" -f $p, $b[0], $b[1], $b[2])
}
