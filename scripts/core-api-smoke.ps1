$ErrorActionPreference = "Stop"
$base = "http://localhost:5080"
$out = Join-Path $PSScriptRoot "..\.smoke-core"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sample = Join-Path $PSScriptRoot "..\samples\hello.pdf"

$email = "core-$(Get-Random)@nam-dev.local"
@{ email = $email; password = "Passw0rd!"; displayName = "Core" } | ConvertTo-Json | Set-Content "$out\auth-body.json" -Encoding utf8
curl.exe -s -X POST "$base/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@$out\auth-body.json" -o "$out\auth.json"
$token = (Get-Content "$out\auth.json" -Raw | ConvertFrom-Json).accessToken
if (-not $token) { throw "auth failed: $(Get-Content "$out\auth.json" -Raw)" }

curl.exe -s -X POST "$base/api/v1/pdf/core/info" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -o "$out\info.json"
$info = Get-Content "$out\info.json" -Raw | ConvertFrom-Json
Write-Host "info pages=$($info.pageCount)"

'{"title":"Core Smoke","author":"Nam-Dev"}' | Set-Content "$out\set-info.json" -Encoding utf8 -NoNewline
curl.exe -s -X POST "$base/api/v1/pdf/core/info/set" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -F "options=<$out\set-info.json" -o "$out\info-set.pdf"
Write-Host "info/set size=$((Get-Item "$out\info-set.pdf").Length)"

'{"header":"Nam-Dev","footer":"core"}' | Set-Content "$out\hf.json" -Encoding utf8 -NoNewline
curl.exe -s -X POST "$base/api/v1/pdf/core/header-footer" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -F "options=<$out\hf.json" -o "$out\hf.pdf"
Write-Host "header-footer size=$((Get-Item "$out\hf.pdf").Length)"

'{"atIndex":1,"count":1}' | Set-Content "$out\blank.json" -Encoding utf8 -NoNewline
curl.exe -s -X POST "$base/api/v1/pdf/core/insert-blank" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -F "options=<$out\blank.json" -o "$out\blank.pdf"
Write-Host "insert-blank size=$((Get-Item "$out\blank.pdf").Length)"

'{"bookmarks":[{"title":"Page1","pageIndex":0,"opened":true}]}' | Set-Content "$out\bm.json" -Encoding utf8 -NoNewline
curl.exe -s -X POST "$base/api/v1/pdf/core/bookmarks/set" -H "Authorization: Bearer $token" -F "file=@$sample;type=application/pdf" -F "options=<$out\bm.json" -o "$out\bm.pdf"
curl.exe -s -X POST "$base/api/v1/pdf/core/bookmarks" -H "Authorization: Bearer $token" -F "file=@$out\bm.pdf;type=application/pdf" -o "$out\bm-list.json"
Write-Host "bookmarks=$((Get-Content "$out\bm-list.json" -Raw))"

Write-Host "OK core-api-smoke"
