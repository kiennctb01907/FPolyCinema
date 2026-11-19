# Chuẩn bị gói deploy FPolyCinema lên MonsterASP.NET (IIS, Windows).
# Cách dùng (PowerShell, đứng ở thư mục gốc dự án):
#   powershell -ExecutionPolicy Bypass -File tools/deploy/monsterasp.ps1
#   (script hỏi chuỗi kết nối; hoặc đặt sẵn $env:FPOLY_DB = "Server=...;Database=...;User Id=...;Password=...;")
# Script làm 4 việc:
#   1. dotnet publish bản Release vào artifacts/publish (bỏ appsettings.Development.json).
#   2. Chỉ khi có -InitFromHere: chạy --init-only từ máy này vào CSDL (cần bật Remote access cho CSDL).
#      Mặc định bỏ qua: ứng dụng tự tạo bảng, stored procedure và nạp dữ liệu mẫu ở lần khởi động đầu trên máy chủ.
#   3. Ghi web.config có ASPNETCORE_ENVIRONMENT=Production và ConnectionStrings__Default.
#   4. Nén thành artifacts/fpolycinema-monsterasp.zip để tải lên.
# Chuỗi kết nối chỉ nằm trong biến môi trường và trong gói zip, không ghi vào mã nguồn.
# Gói zip có chứa mật khẩu CSDL: không gửi gói này cho người khác.
param([switch]$InitFromHere)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$publish = Join-Path $root 'artifacts\publish'
$zip = Join-Path $root 'artifacts\fpolycinema-monsterasp.zip'

function Test-Conn([string]$c) {
    -not [string]::IsNullOrWhiteSpace($c) -and $c -match 'Server=' -and $c -match 'Password=' -and $c -notmatch 'Password=\*'
}
$conn = $env:FPOLY_DB
if ([string]::IsNullOrWhiteSpace($conn)) {
    Write-Host 'Chép chuỗi kết nối CSDL (MonsterASP > Databases > Manage > Connection string > Show with password, có cả mật khẩu).' -ForegroundColor Yellow
    Write-Host 'Rồi dán vào đây và Enter, hoặc chỉ cần nhấn Enter để script tự lấy từ clipboard:' -ForegroundColor Yellow
    $secure = Read-Host -AsSecureString
    $conn = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    # Dán vào ô ẩn hay bị cắt hoặc lẫn xuống dòng: khi đó lấy thẳng từ clipboard
    if (-not (Test-Conn $conn)) { $conn = (Get-Clipboard -Raw) }
}
if ($conn) { $conn = ($conn -replace '[\r\n]+', ' ').Trim() }
if (-not (Test-Conn $conn)) {
    Write-Host 'Chuỗi kết nối trống hoặc chưa có mật khẩu thật (bấm Show with password trước khi chép).' -ForegroundColor Red
    exit 1
}
# MonsterASP dùng chứng chỉ riêng: tin chứng chỉ máy chủ nếu chuỗi chưa nói gì
if ($conn -notmatch 'TrustServerCertificate') { $conn = $conn.TrimEnd(';') + ';TrustServerCertificate=True' }

Write-Host '== 1/4 Publish bản Release' -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
dotnet publish (Join-Path $root 'src\FPolyCinema.Web') -c Release -o $publish
if ($LASTEXITCODE -ne 0) { exit 1 }
$devSettings = Join-Path $publish 'appsettings.Development.json'
if (Test-Path $devSettings) { Remove-Item -Force $devSettings }

if (-not $InitFromHere) {
    Write-Host '== 2/4 Bỏ qua: ứng dụng tự khởi tạo CSDL ở lần chạy đầu trên máy chủ' -ForegroundColor Cyan
} else {
Write-Host '== 2/4 Khởi tạo CSDL từ máy này (lần đầu nạp dữ liệu mẫu, có thể mất vài phút)' -ForegroundColor Cyan
$oldEnv = $env:ASPNETCORE_ENVIRONMENT
$oldConn = $env:ConnectionStrings__Default
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ConnectionStrings__Default = $conn
Push-Location $publish
try {
    dotnet .\FPolyCinema.Web.dll --init-only
    $initCode = $LASTEXITCODE
} finally {
    Pop-Location
    $env:ASPNETCORE_ENVIRONMENT = $oldEnv
    $env:ConnectionStrings__Default = $oldConn
}
if ($initCode -ne 0) {
    Write-Host 'Khởi tạo CSDL thất bại: xem lỗi ở trên (thường là sai chuỗi kết nối hoặc chưa bật truy cập từ xa cho CSDL).' -ForegroundColor Red
    exit 1
}
}

Write-Host '== 3/4 Ghi web.config' -ForegroundColor Cyan
$connXml = [System.Security.SecurityElement]::Escape($conn)
$webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\FPolyCinema.Web.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" startupTimeLimit="300">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="ConnectionStrings__Default" value="$connXml" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
[System.IO.File]::WriteAllText((Join-Path $publish 'web.config'), $webConfig, (New-Object System.Text.UTF8Encoding($false)))

Write-Host '== 4/4 Nén gói tải lên' -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip

Write-Host ''
Write-Host "Xong. Gói tải lên: $zip" -ForegroundColor Green
Write-Host 'Trên MonsterASP: mở File Manager của website, xóa file mặc định trong wwwroot, tải zip lên, giải nén ngay tại wwwroot (web.config phải nằm ngay trong wwwroot).'
