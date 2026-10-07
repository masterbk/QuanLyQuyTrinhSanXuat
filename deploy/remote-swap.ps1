# remote-swap.ps1 — chạy TRÊN SERVER (VPS Windows) để tráo bản build mới.
# Được deploy.ps1 ở máy local gọi qua SSH. KHÔNG chạy tay trừ khi hiểu rõ.
#
# Nguyên tắc an toàn:
#   - Đặt app_offline.htm để IIS nhả khóa DLL trước khi ghi đè.
#   - robocopy KHÔNG /PURGE: mọi file chỉ-có-trên-server được giữ nguyên
#     (appsettings.json chứa secret production, wwwroot\uploads, firebase json, googles...).
#   - /XF, /XD thêm 1 lớp chặn nữa cho các file nhạy cảm, phòng khi lọt vào gói.
param(
    [Parameter(Mandatory = $true)][string]$AppDir,       # thư mục app trên IIS (deploy.ps1 truyền vào)
    [Parameter(Mandatory = $true)][string]$Incoming,     # đường dẫn file .zip đã upload
    [string]$Staging = "C:\deploy\staging",
    [string]$BackupDir = "C:\deploy\backup"
)

$ErrorActionPreference = "Stop"
function Log($m) { Write-Output ("[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }

if (-not (Test-Path $AppDir))   { throw "Khong thay thu muc app: $AppDir" }
if (-not (Test-Path $Incoming)) { throw "Khong thay goi: $Incoming" }

$offline = Join-Path $AppDir "app_offline.htm"

try {
    # 1) Bung goi ra staging truoc (chua dong gi vao app) ---------------------
    if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
    New-Item -ItemType Directory -Path $Staging -Force | Out-Null
    Log "Bung goi -> $Staging"
    Expand-Archive -Path $Incoming -DestinationPath $Staging -Force
    if (-not (Test-Path (Join-Path $Staging "HCP.Web.dll"))) {
        throw "Goi khong hop le: thieu HCP.Web.dll"
    }

    # 2) Sao luu nhanh cac DLL/app hien tai (khong gom uploads cho nhe) --------
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $bk = Join-Path $BackupDir $stamp
    New-Item -ItemType Directory -Path $bk -Force | Out-Null
    Log "Sao luu hien trang -> $bk"
    robocopy $AppDir $bk /E /NFL /NDL /NP /R:1 /W:1 /XD "$AppDir\wwwroot\uploads" | Out-Null
    # giu toi da 5 ban backup gan nhat
    Get-ChildItem $BackupDir -Directory | Sort-Object Name -Descending |
        Select-Object -Skip 5 | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    # 3) Nha khoa DLL bang app_offline ---------------------------------------
    Log "Dat app_offline.htm"
    Set-Content -Path $offline -Encoding UTF8 -Value @"
<!doctype html><html lang="vi"><head><meta charset="utf-8">
<meta http-equiv="refresh" content="20"><title>Dang cap nhat</title></head>
<body style="font-family:sans-serif;text-align:center;padding-top:15%">
<h2>He thong dang cap nhat</h2><p>Vui long quay lai sau it phut.</p></body></html>
"@
    Start-Sleep -Seconds 3

    # 4) Copy de len app, GIU LAI config + uploads ---------------------------
    Log "robocopy -> $AppDir (giu config + uploads)"
    $xf = @("app_offline.htm","appsettings.json","appsettings.Development.json",
            "appsettings.Production.json","firebase-adminsdk.json")
    robocopy $Staging $AppDir /E /NFL /NDL /NP /R:3 /W:3 `
        /XF $xf `
        /XD "$AppDir\wwwroot\uploads" | Out-Null
    $rc = $LASTEXITCODE
    if ($rc -ge 8) { throw "robocopy loi, ma thoat = $rc" }
    Log "robocopy OK (ma thoat $rc)"
}
finally {
    # 5) Luon go app_offline de site song lai --------------------------------
    if (Test-Path $offline) { Remove-Item $offline -Force; Log "Go app_offline" }
    if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $Incoming){ Remove-Item $Incoming -Force -ErrorAction SilentlyContinue }
}

Log "DEPLOY OK"
