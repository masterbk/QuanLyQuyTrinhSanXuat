# deploy.ps1 — chạy TRÊN MÁY LOCAL (Windows) để deploy HCP.Web len VPS.
#
# Dung:   pwsh -File deploy\deploy.ps1
#         pwsh -File deploy\deploy.ps1 -SkipBuild        (dung lai ban publish cu)
#
# Luong:  dotnet publish -> loai file secret -> nen zip -> scp len VPS
#         -> goi remote-swap.ps1 (nha khoa, robocopy giu config+uploads, bo app_offline)
#
# Yeu cau: da co SSH key ~/.ssh/tuannghia_deploy nhan voi VPS (dang nhap khong mat khau).

param(
    [string]$VpsHost,
    [string]$VpsUser,
    [int]   $Port     = 0,
    [string]$Key,
    [string]$AppDir,
    [string]$RemoteDir= "C:/deploy",                 # noi tam tren VPS (dung / cho scp)
    [string]$Project  = "src\HCP.Web\HCP.Web.csproj",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot        # thu muc goc repo
Set-Location $repo
function Log($m) { Write-Host ("[deploy] {0}" -f $m) -ForegroundColor Cyan }

# --- 0) Nap cau hinh ha tang tu file local (deploy\deploy.local.ps1, KHONG commit) ---
# File do tra ve 1 hashtable; xem deploy.local.ps1.example. Tham so truyen tay se uu tien.
$cfg = Join-Path $PSScriptRoot "deploy.local.ps1"
if (Test-Path $cfg) {
    $local = & $cfg
    if (-not $VpsHost -and $local.VpsHost) { $VpsHost = $local.VpsHost }
    if (-not $VpsUser -and $local.VpsUser) { $VpsUser = $local.VpsUser }
    if ($Port -eq 0  -and $local.Port)     { $Port    = [int]$local.Port }
    if (-not $Key     -and $local.Key)     { $Key     = $local.Key }
    if (-not $AppDir  -and $local.AppDir)  { $AppDir  = $local.AppDir }
}
if ($Port -eq 0) { $Port = 22 }
if (-not $Key) { $Key = "$env:USERPROFILE\.ssh\tuannghia_deploy" }
if (-not $VpsHost -or -not $VpsUser -or -not $AppDir) {
    throw "Thieu cau hinh VPS. Tao deploy\deploy.local.ps1 tu deploy.local.ps1.example (hoac truyen -VpsHost -VpsUser -AppDir)."
}
Log "Dich: $VpsUser@${VpsHost}:$Port  ->  $AppDir"

$pub = Join-Path $env:TEMP "hcp-publish"
$zip = Join-Path $env:TEMP "hcp-app.zip"

# --- 1) Build + publish --------------------------------------------------
if (-not $SkipBuild) {
    if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
    Log "dotnet publish (Release)..."
    dotnet publish $Project -c Release -o $pub --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish that bai" }
}
elseif (-not (Test-Path (Join-Path $pub "HCP.Web.dll"))) {
    throw "Chua co ban publish de SkipBuild — bo -SkipBuild de build lai"
}

# --- 2) Loai file bi mat / cau hinh khoi goi -----------------------------
# KHONG day cac file nay: server tu giu ban production cua no.
$loai = @("appsettings.json","appsettings.Development.json","appsettings.Production.json",
          "appsettings.*.Local.json","firebase-adminsdk.json","web.config")
foreach ($f in $loai) {
    Get-ChildItem $pub -Filter $f -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Force; Log "loai khoi goi: $($_.Name)"
    }
}

# --- 3) Nen -------------------------------------------------------------
if (Test-Path $zip) { Remove-Item $zip -Force }
Log "Nen goi..."
Compress-Archive -Path (Join-Path $pub "*") -DestinationPath $zip -Force
$mb = "{0:N1}" -f ((Get-Item $zip).Length / 1MB)
Log "Goi: $zip ($mb MB)"

# --- 4) Chuyen len VPS --------------------------------------------------
$sshOpt = @("-i", $Key, "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=accept-new", "-p", "$Port")
$scpOpt = @("-i", $Key, "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=accept-new", "-P", "$Port")
$dest   = "$VpsUser@$VpsHost"

Log "Tao thu muc tam tren VPS..."
# Luu y: cac duong dan khong co khoang trang -> khong can nhay long (PowerShell 7 giu nguyen).
& ssh @sshOpt $dest "powershell -NoProfile -Command New-Item -ItemType Directory -Force -Path $RemoteDir"
if ($LASTEXITCODE -ne 0) { throw "Khong tao duoc $RemoteDir tren VPS" }

Log "Upload goi + script..."
& scp @scpOpt $zip "${dest}:$RemoteDir/hcp-app.zip"
if ($LASTEXITCODE -ne 0) { throw "scp goi that bai" }
& scp @scpOpt (Join-Path $PSScriptRoot "remote-swap.ps1") "${dest}:$RemoteDir/remote-swap.ps1"
if ($LASTEXITCODE -ne 0) { throw "scp script that bai" }

# --- 5) Trao doi ban moi tren VPS ---------------------------------------
Log "Chay remote-swap tren VPS..."
# Duong dan khong co khoang trang -> truyen thang, khong nhay long.
$remoteCmd = "powershell -NoProfile -ExecutionPolicy Bypass -File $RemoteDir/remote-swap.ps1 " +
             "-AppDir $AppDir -Incoming $RemoteDir/hcp-app.zip"
& ssh @sshOpt $dest $remoteCmd
if ($LASTEXITCODE -ne 0) { throw "remote-swap bao loi" }

Log "XONG. Kiem tra: https://quanly.tuannghiabakery.vn"
