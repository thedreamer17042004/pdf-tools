$ErrorActionPreference = "Stop"
$base = "http://localhost:5080"
$out = Join-Path $PSScriptRoot "..\.smoke-word"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$email = "word-$(Get-Random)@nam-dev.local"
@{ email = $email; password = "Passw0rd!"; displayName = "Word" } | ConvertTo-Json | Set-Content "$out\auth-body.json" -Encoding utf8
curl.exe -s -X POST "$base/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@$out\auth-body.json" -o "$out\auth.json"
$token = (Get-Content "$out\auth.json" -Raw | ConvertFrom-Json).accessToken
if (-not $token) { throw "auth failed" }

curl.exe -s "$base/api/v1/word/core" -o "$out\catalog.json"
Write-Host "catalog ok"

curl.exe -s -X POST "$base/api/v1/word/core/create" -H "Authorization: Bearer $token" --form-string "text=Hello {{FullName}}`nDate: «Date»" -F "title=Template" -o "$out\template.docx"
Write-Host "create=$((Get-Item "$out\template.docx").Length)"

curl.exe -s -X POST "$base/api/v1/word/core/merge-fields/list" -H "Authorization: Bearer $token" -F "file=@$out\template.docx" -o "$out\fields.json"
Write-Host "fields=$((Get-Content "$out\fields.json" -Raw))"

'{"values":{"FullName":"Ada Lovelace","Date":"2026-11-14"}}' | Set-Content "$out\merge.json" -Encoding utf8 -NoNewline
curl.exe -s -X POST "$base/api/v1/word/core/merge-fields" -H "Authorization: Bearer $token" -F "file=@$out\template.docx" -F "options=<$out\merge.json" -o "$out\merged.docx"
Write-Host "merged=$((Get-Item "$out\merged.docx").Length)"

curl.exe -s -X POST "$base/api/v1/word/core/to-pdf" -H "Authorization: Bearer $token" -F "file=@$out\merged.docx" -o "$out\from-word.pdf"
$pdfMagic = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$out\from-word.pdf")[0..3])
Write-Host "to-pdf=$pdfMagic size=$((Get-Item "$out\from-word.pdf").Length)"

Write-Host "OK word-core-smoke"
