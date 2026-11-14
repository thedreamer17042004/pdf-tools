$ErrorActionPreference = "Stop"
$base = "http://localhost:5080"
$sample = "D:\Code\learning\PDF-CORE\samples\hello.pdf"
$out = "D:\Code\learning\PDF-CORE\data"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$email = "final_$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())@ex.com"
$authBody = @{ email = $email; password = "Final!Test1"; displayName = "Final" } | ConvertTo-Json -Compress
Set-Content "$out\auth-body.json" $authBody -Encoding utf8
curl.exe -s -X POST "$base/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@$out\auth-body.json" -o "$out\auth.json"
$token = (Get-Content "$out\auth.json" | ConvertFrom-Json).accessToken
Write-Host "auth=ok"

curl.exe -s -F "file=@$sample" -H "Authorization: Bearer $token" "$base/api/v1/documents" -o "$out\up.json"
$id = (Get-Content "$out\up.json" | ConvertFrom-Json).id
Write-Host "document=$id owner=$((Get-Content "$out\up.json" | ConvertFrom-Json).ownerUserId)"

curl.exe -s "$base/api/v1/documents/mine" -H "Authorization: Bearer $token" -o "$out\mine.json"
Write-Host "mineCount=$((Get-Content "$out\mine.json" | ConvertFrom-Json).items.Count)"

curl.exe -s "$base/api/v1/documents/$id/text" -o "$out\text.json"
Write-Host "textPages=$((Get-Content "$out\text.json" | ConvertFrom-Json).pages.Count)"

function Wait-Job([string]$jobId) {
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 400
    curl.exe -s "$base/api/v1/jobs/$jobId" -H "Authorization: Bearer $token" -o "$out\job.json"
    $j = Get-Content "$out\job.json" | ConvertFrom-Json
    if ($j.status -eq "Succeeded" -or $j.status -eq "Failed") { return $j }
  }
  throw "timeout $jobId"
}

$wm = @{ documentId = $id; text = "Nam-Dev"; opacity = 0.3 } | ConvertTo-Json -Compress
Set-Content "$out\wm.json" $wm -Encoding utf8
curl.exe -s -X POST "$base/api/v1/pdf/watermark" -H "Authorization: Bearer $token" -H "Content-Type: application/json" --data-binary "@$out\wm.json" -o "$out\wm-job.json"
$wmr = Wait-Job ((Get-Content "$out\wm-job.json" | ConvertFrom-Json).id)
Write-Host "watermark=$($wmr.status)"

$pr = @{ documentId = $id; userPassword = "secret1" } | ConvertTo-Json -Compress
Set-Content "$out\pr.json" $pr -Encoding utf8
curl.exe -s -X POST "$base/api/v1/pdf/protect" -H "Authorization: Bearer $token" -H "Content-Type: application/json" --data-binary "@$out\pr.json" -o "$out\pr-job.json"
$prr = Wait-Job ((Get-Content "$out\pr-job.json" | ConvertFrom-Json).id)
Write-Host "protect=$($prr.status)"
