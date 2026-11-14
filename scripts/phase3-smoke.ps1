$ErrorActionPreference = "Stop"
$base = "http://localhost:5080"
$sample = "D:\Code\learning\PDF-CORE\samples\hello.pdf"
$out = "D:\Code\learning\PDF-CORE\data"

curl.exe -s -F "file=@$sample" "$base/api/v1/documents" -o "$out\up.json"
$id = (Get-Content "$out\up.json" | ConvertFrom-Json).id
Write-Host "document=$id"

$annBody = @{
  annotations = @(
    @{
      type = "Highlight"
      pageIndex = 0
      x = 72
      y = 700
      width = 120
      height = 18
      color = "#FFFF00"
      opacity = 0.35
      contents = "phase3"
    }
  )
} | ConvertTo-Json -Depth 5 -Compress
Set-Content -Path "$out\ann-body.json" -Value $annBody -Encoding utf8
curl.exe -s -X PUT "$base/api/v1/documents/$id/annotations" -H "Content-Type: application/json" --data-binary "@$out\ann-body.json" -o "$out\ann.json"
Write-Host "annotations=$((Get-Content "$out\ann.json" | ConvertFrom-Json).items.Count)"

curl.exe -s "$base/api/v1/documents/$id/annotations/xfdf" -o "$out\out.xfdf"
$has = Select-String -Path "$out\out.xfdf" -Pattern "highlight" -Quiet
Write-Host "xfdf_highlight=$has"

$email = "p3_$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())@ex.com"
$authBody = @{ email = $email; password = "Phase3!Test"; displayName = "P3" } | ConvertTo-Json -Compress
Set-Content "$out\auth-body.json" $authBody -Encoding utf8
curl.exe -s -X POST "$base/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@$out\auth-body.json" -o "$out\auth.json"
$token = (Get-Content "$out\auth.json" | ConvertFrom-Json).accessToken

function Wait-Job([string]$jobId) {
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 400
    curl.exe -s "$base/api/v1/jobs/$jobId" -H "Authorization: Bearer $token" -o "$out\job.json"
    $j = Get-Content "$out\job.json" | ConvertFrom-Json
    if ($j.status -eq "Succeeded" -or $j.status -eq "Failed") { return $j }
  }
  throw "timeout $jobId"
}

$compBody = @{ documentId = $id; removeMetadata = $true } | ConvertTo-Json -Compress
Set-Content "$out\comp-body.json" $compBody -Encoding utf8
curl.exe -s -X POST "$base/api/v1/pdf/compress" -H "Authorization: Bearer $token" -H "Content-Type: application/json" --data-binary "@$out\comp-body.json" -o "$out\comp.json"
$comp = Wait-Job ((Get-Content "$out\comp.json" | ConvertFrom-Json).id)
Write-Host "compress=$($comp.status) file=$($comp.resultFileName)"

$orgBody = @{ documentId = $id; pageOrder = @(1, 0) } | ConvertTo-Json -Compress
Set-Content "$out\org-body.json" $orgBody -Encoding utf8
curl.exe -s -X POST "$base/api/v1/pdf/organize" -H "Authorization: Bearer $token" -H "Content-Type: application/json" --data-binary "@$out\org-body.json" -o "$out\org.json"
$org = Wait-Job ((Get-Content "$out\org.json" | ConvertFrom-Json).id)
Write-Host "organize=$($org.status) file=$($org.resultFileName)"
