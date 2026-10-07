# Deploy tự động lên VPS

Deploy `HCP.Web` từ máy local lên VPS Windows (`quanly.tuannghiabakery.vn`) bằng 1 lệnh.

## Cách chạy

```powershell
pwsh -File deploy\deploy.ps1
```

Tùy chọn:
- `-SkipBuild` — dùng lại bản publish trước đó (không build lại), deploy nhanh hơn.
- `-VpsHost`, `-VpsUser`, `-Port`, `-Key`, `-AppDir` — ghi đè mặc định nếu cần.

## Nó làm gì

1. `dotnet publish -c Release` ra thư mục tạm.
2. **Loại khỏi gói** các file cấu hình/secret: `appsettings.json`, `appsettings.*.json`,
   `firebase-adminsdk.json`, `web.config` → **không bao giờ đè bản production trên server**.
3. Nén rồi `scp` lên `C:\deploy` trên VPS.
4. Gọi `remote-swap.ps1` trên VPS:
   - Bung gói ra staging, kiểm tra có `HCP.Web.dll`.
   - Sao lưu hiện trạng vào `C:\deploy\backup\<thời gian>` (giữ 5 bản gần nhất, không gồm uploads).
   - Đặt `app_offline.htm` để IIS nhả khóa DLL.
   - `robocopy` **không /PURGE** (giữ lại `appsettings.json`, `wwwroot\uploads`, firebase json, googles...).
   - Gỡ `app_offline.htm` → site sống lại; migration EF tự áp khi khởi động.

## An toàn

- Dùng SSH key `~/.ssh/tuannghia_deploy` (không mật khẩu). Private key chỉ nằm ở máy local.
- Không file secret nào bị đẩy lên; cấu hình production nằm nguyên trong `appsettings.json` trên server.
- `wwwroot\uploads` (ảnh thật, ~67 MB) luôn được giữ.
- Có backup DLL trước mỗi lần deploy, có thể khôi phục thủ công từ `C:\deploy\backup`.

## Khôi phục (nếu bản mới lỗi)

Trên VPS, copy ngược bản backup gần nhất đè lại app rồi gỡ `app_offline.htm`:

```powershell
$bk = (Get-ChildItem C:\deploy\backup -Directory | Sort-Object Name -Desc)[0].FullName
robocopy $bk C:\www\tuannghiabakery.vn /E /XD "C:\www\tuannghiabakery.vn\wwwroot\uploads"
```
