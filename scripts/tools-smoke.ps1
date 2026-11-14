$ErrorActionPreference = "Stop"
$base = "http://localhost:5080"
$sample = "D:\Code\learning\PDF-CORE\samples\hello.pdf"
$out = "D:\Code\learning\PDF-CORE\data"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$email = "tools_$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())@ex.com"
Set-Content "$out\auth-body.json" (@{ email=$email; password="Tools!Test1"; displayName="Tools" } | ConvertTo-Json -Compress) -Encoding utf8
curl.exe -s -X POST "$base/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@$out\auth-body.json" -o "$out\auth.json"
$token = (Get-Content "$out\auth.json" | ConvertFrom-Json).accessToken

curl.exe -s -F "file=@$sample" -H "Authorization: Bearer $token" "$base/api/v1/documents" -o "$out\up.json"
$id = (Get-Content "$out\up.json" | ConvertFrom-Json).id

function Wait-Job([string]$jobId) {
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 400
    curl.exe -s "$base/api/v1/jobs/$jobId" -H "Authorization: Bearer $token" -o "$out\job.json"
    $j = Get-Content "$out\job.json" | ConvertFrom-Json
    if ($j.status -eq "Succeeded" -or $j.status -eq "Failed") { return $j }
  }
  throw "timeout"
}

function Run([string]$path, $body) {
  $file = "$out\req.json"
  Set-Content $file ($body | ConvertTo-Json -Compress -Depth 5) -Encoding utf8
  curl.exe -s -X POST "$base$path" -H "Authorization: Bearer $token" -H "Content-Type: application/json" --data-binary "@$file" -o "$out\job-start.json"
  $jid = (Get-Content "$out\job-start.json" | ConvertFrom-Json).id
  return Wait-Job $jid
}

$r = Run "/api/v1/pdf/reverse" @{ documentId = $id }
Write-Host "reverse=$($r.status)"
$r = Run "/api/v1/pdf/extract-pages" @{ documentId = $id; pageIndexes = @(0) }
Write-Host "extract=$($r.status)"
$r = Run "/api/v1/pdf/delete-pages" @{ documentId = $id; pageIndexes = @(0) }
Write-Host "delete=$($r.status)"
$r = Run "/api/v1/pdf/page-numbers" @{ documentId = $id; format = "{n}/{total}" }
Write-Host "numbers=$($r.status)"
$r = Run "/api/v1/pdf/to-images" @{ documentId = $id; dpi = 96 }
Write-Host "toImages=$($r.status) file=$($r.resultFileName)"
