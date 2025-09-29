$path = 'd:\Kodehode\O-Sim\.github\workflows\docker-build-push.yml'
$lineNumber = 70
$line = (Get-Content $path)[$lineNumber - 1]
Write-Output ("Line {0}: [{1}]" -f $lineNumber, $line)
$chars = $line.ToCharArray()
for ($i = 0; $i -lt $chars.Length; $i++) {
    $c = $chars[$i]
    $code = [int][char]$c
    $idx = $i + 1
    Write-Host ("{0,3}: '{1}' 0x{2:X4}" -f $idx, $c, $code)
}