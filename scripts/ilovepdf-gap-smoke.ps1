$ErrorActionPreference = "Stop"
$base = "http://localhost:5080"
$out = Join-Path $PSScriptRoot "..\.smoke-gap"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sample = Join-Path $PSScriptRoot "..\samples\hello.pdf"

$email = "gap-$(Get-Random)@nam-dev.local"
@{ email = $email; password = "Passw0rd!"; displayName = "Gap" } | ConvertTo-Json | Set-Content "$out\auth-body.json" -Encoding utf8
curl.exe -s -X POST "$base/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@$out\auth-body.json" -o "$out\auth.json"
$token = (Get-Content "$out\auth.json" -Raw | ConvertFrom-Json).accessToken
if (-not $token) { throw "auth failed" }

curl.exe -s -X POST "$base/api/v1/pdf/core/repair" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -o "$out\repaired.pdf"
Write-Host "repair=$((Get-Item "$out\repaired.pdf").Length)"

curl.exe -s -X POST "$base/api/v1/pdf/core/to-markdown" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -o "$out\export.md"
Write-Host "markdown chars=$((Get-Content "$out\export.md" -Raw).Length)"

curl.exe -s -X POST "$base/api/v1/pdf/core/compare" -H "Authorization: Bearer $token" -F "left=@$sample;type=application/pdf" -F "right=@$sample;type=application/pdf" -o "$out\compare.json"
Write-Host "compare=$((Get-Content "$out\compare.json" -Raw))"

curl.exe -s -X POST "$base/api/v1/pdf/core/html-to-pdf" -H "Authorization: Bearer $token" --form-string "html=<h1>Hi</h1><p>Nam-Dev</p>" -F "title=Smoke" -o "$out\html.pdf"
Write-Host "html-to-pdf=$((Get-Item "$out\html.pdf").Length)"

'{"pageIndex":0,"xPoints":72,"yPoints":72,"signerName":"Ada"}' | Set-Content "$out\sign.json" -Encoding utf8 -NoNewline
curl.exe -s -X POST "$base/api/v1/pdf/core/sign-stamp" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -F "options=<$out\sign.json" -o "$out\signed.pdf"
Write-Host "sign-stamp=$((Get-Item "$out\signed.pdf").Length)"

Write-Host "OK ilovepdf-gap-smoke"
