# Kế hoạch xây dựng nền tảng quản lý quy trình sản xuất đa cơ sở
### (SaaS đồng bộ dữ liệu sang HanoiCheck)

**Xác nhận vai trò hệ thống (đã điều chỉnh sau trao đổi):**
Đây **không phải** phần mềm nội bộ một công ty, mà là **nền tảng SaaS đa khách hàng (multi-tenant)**:
- Mỗi **cơ sở sản xuất** (bếp suất ăn, xưởng bánh ngọt, xưởng bánh mì...) là **một khách hàng (tenant) độc lập** = **một Nhà cung cấp (NCC)** riêng theo định nghĩa của HanoiCheck, có **bộ OAuth credential riêng** (`client_id`/`client_secret`/`hmac_secret`) do chính HanoiCheck cấp cho họ (qua quy trình đăng ký 5 bước ở tài liệu hướng dẫn).
- Cơ sở **tự đăng ký** tài khoản trên nền tảng của bạn → **bạn (platform admin) duyệt** kích hoạt → cơ sở tự nhập thông tin quy trình sản xuất của họ (kho, nhân sự, lô hàng, món ăn, đơn hàng...) → hệ thống **tự động đẩy đồng bộ** sang HanoiCheck bằng credential riêng của cơ sở đó, không cần họ bấm gửi thủ công.
- Mỗi cơ sở **chỉ thấy dữ liệu của chính mình** (cách ly hoàn toàn với cơ sở khác). Bạn (platform admin) có góc nhìn tổng để giám sát tình trạng đồng bộ toàn hệ thống.
- Phạm vi giai đoạn 1: đủ chức năng để mỗi cơ sở nhập được **đầy đủ dữ liệu HanoiCheck yêu cầu** — chưa cần mobile app hay cổng đặt hàng.

---

## 0. Ràng buộc quan trọng rút ra từ tài liệu (không đổi so với phân tích trước)

### Từ "Đặc tả API"
- 2 lớp xác thực bắt buộc: **OAuth2 Client Credentials** (`POST /supplier/token`, refresh 14 ngày) + **chữ ký HMAC-SHA256** trên 3 header `X-Timestamp/X-Nonce/X-Signature` (canonical string `METHOD\nPATH\nTIMESTAMP\nNONCE\nSHA256_HEX(raw_body)`).
- **12 endpoint**: 5 đồng bộ ngay (200) — kho, khâu, quy trình, nhân sự, NCC đầu vào; 5 bất đồng bộ (202) — cơ sở, thực phẩm, lô sản xuất, món ăn, đơn hàng; 1 read-only (danh mục thực phẩm chuẩn).
- Mọi endpoint `merge` nhận **mảng bản ghi**, upsert theo khóa nghiệp vụ (vd `ma_kho`, `ma_san_pham`).
- Lỗi validate trả **422** dạng `errors: {"field": ["msg"]}`.
- **Quan trọng cho multi-tenant:** `client_id được cấp khi tạo OAuth Client cho NCC` → **mỗi tenant (NCC) có 1 bộ credential riêng biệt**, hệ thống phải lưu & dùng đúng credential của từng tenant khi ký request, không được dùng chung.

### Từ "Tài liệu hướng dẫn HnC"
- Mỗi cơ sở muốn kết nối phải tự qua **quy trình đăng ký 5 bước với cơ quan quản lý nhà nước** để được **HnC cấp riêng** client_id/client_secret/hmac_secret — đây là bước **ngoài phạm vi phần mềm của bạn**, tenant tự làm hồ sơ rồi **nhập credential đó vào phần cài đặt tenant** trên nền tảng của bạn.
- Log lưu tối thiểu 2 năm, dữ liệu nhạy cảm phải mã hóa, retry tự động khi gửi lỗi, không xóa dữ liệu gốc phía HnC, HTTPS bắt buộc.

---

## 1. Mô hình Multi-tenant (phần thay đổi cốt lõi)

### 1.1. Đơn vị tenant
```
Tenant (= 1 Cơ sở sản xuất = 1 NCC độc lập trên HanoiCheck)
 ├─ HnC Credentials: client_id, client_secret, hmac_secret, base_url (sandbox/production)
 ├─ Trạng thái: PendingApproval → Active → Suspended
 ├─ Người dùng nội bộ của tenant: TenantAdmin, TenantStaff (kho/sản xuất/giao hàng/kế toán)
 └─ Toàn bộ dữ liệu nghiệp vụ (Kho, Cơ sở*, Khâu, Quy trình, Nhân sự, NCC đầu vào,
     Thực phẩm, Lô sản xuất, Món ăn, Đơn hàng) đều gắn TenantId

* Lưu ý: "Cơ sở" (ma_co_so) trong spec là sub-entity bên trong 1 NCC (endpoint facilities/merge).
  Một Tenant có thể khai báo 1-N "Cơ sở" vật lý của riêng họ (vd vừa có bếp suất ăn vừa có xưởng bánh),
  tất cả vẫn đồng bộ dưới CÙNG 1 bộ credential của tenant đó.
```

### 1.2. Chiến lược cách ly dữ liệu
- **Shared database, shared schema, cột `TenantId`** trên mọi bảng nghiệp vụ (không tách DB riêng từng tenant — tốn công vận hành, không hợp lý cho 1 dev quản lý hàng chục tenant).
- Dùng thư viện **Finbuckle.MultiTenant** (chuẩn cho ASP.NET Core/EF Core) để:
  - Tự động gắn `TenantId` khi ghi dữ liệu.
  - Tự động lọc `WHERE TenantId = @current` trên mọi query (EF Core Global Query Filter) — chống lộ dữ liệu chéo tenant do quên `Where()`.
  - Resolve tenant theo **claim trong JWT/cookie sau khi đăng nhập** (không cần subdomain riêng từng tenant — đơn giản hơn cho MVP).
- **2 tầng vai trò (role) rõ ràng:**
  | Tầng | Vai trò | Phạm vi |
  |---|---|---|
  | Platform (công ty bạn) | `PlatformSuperAdmin` | Duyệt/khóa tenant, xem dashboard đồng bộ toàn hệ thống, xem log lỗi mọi tenant, KHÔNG sửa dữ liệu nghiệp vụ của tenant |
  | Tenant (từng cơ sở) | `TenantAdmin` | Quản lý người dùng nội bộ cơ sở, cấu hình credential HnC, xem toàn bộ dữ liệu + nhật ký đồng bộ của cơ sở mình |
  | Tenant (từng cơ sở) | `TenantStaff` (Kho/Sản xuất/Giao hàng/Kế toán) | Nhập liệu theo phân hệ được cấp quyền |

### 1.3. Luồng đăng ký & kích hoạt
1. Cơ sở vào trang **Đăng ký công khai** (không cần đăng nhập trước) → điền: tên đơn vị, mã số thuế, người đại diện, email/SĐT liên hệ, (tùy chọn) đính kèm giấy phép ATTP.
2. Hệ thống tạo `Tenant` ở trạng thái **PendingApproval** + tài khoản `TenantAdmin` đầu tiên (chưa login được).
3. **PlatformSuperAdmin** (bạn) vào màn "Duyệt đăng ký", xem hồ sơ, **Duyệt** hoặc **Từ chối** (kèm lý do).
4. Duyệt xong → hệ thống gửi email kích hoạt cho `TenantAdmin` → họ đặt mật khẩu → đăng nhập.
5. Sau khi login, `TenantAdmin` vào màn **Cài đặt kết nối HanoiCheck** để tự nhập `client_id/client_secret/hmac_secret/base_url` mà họ đã xin được từ quy trình đăng ký riêng với HnC (ngoài phạm vi hệ thống bạn). **Trước khi nhập đủ credential, hệ thống cho phép nhập liệu nghiệp vụ nhưng KHÔNG đồng bộ** (job đồng bộ tự bỏ qua tenant chưa cấu hình xong, đánh dấu trạng thái "Chưa kết nối HnC").
6. Có nút **"Kiểm tra kết nối"** (gọi thử `POST /supplier/token`) để tenant tự xác nhận credential đúng trước khi dữ liệu thật được đẩy đi.

---

## 2. Kiến trúc tổng thể

```
┌───────────────────────────────────────┐
│ ASP.NET Core 8 (Blazor Server +        │  ← 2 khu vực UI:
│ MudBlazor) + Finbuckle.MultiTenant     │    /admin  (Platform)  /app (Tenant portal)
└───────────────────┬────────────────────┘
                     │ EF Core 8 (Global Query Filter theo TenantId)
┌───────────────────▼────────────────────┐
│  SQL Server (shared DB, cột TenantId)   │
│  + bảng SyncOutbox, TenantHnCCredential │
└───────────────────┬────────────────────┘
                     │ đọc outbox theo từng tenant
┌───────────────────▼────────────────────┐
│  Hangfire worker (xử lý theo TenantId)  │  ← ký HMAC bằng credential CỦA TENANT ĐÓ
└───────────────────┬────────────────────┘
                     │ HTTPS + OAuth2 (per-tenant token) + HMAC (per-tenant secret)
┌───────────────────▼────────────────────┐
│           HanoiCheck API                │
└──────────────────────────────────────────┘
```

**Tech stack (giữ nguyên phần đã chọn, bổ sung phần multi-tenant):**
| Thành phần | Lựa chọn |
|---|---|
| UI | Blazor Server + MudBlazor, 2 khu vực route: `/admin/**` (Platform) và `/app/**` (Tenant portal) |
| Multi-tenancy | **Finbuckle.MultiTenant** + EF Core Global Query Filter theo `TenantId` |
| ORM | EF Core 8, Code-First Migrations |
| Background job | Hangfire (SQL Server storage), job nhận diện theo `TenantId` |
| Gọi API ngoài | `HttpClientFactory` + Polly, **1 HttpClient config theo credential của tenant tại thời điểm gọi** (không cache credential tĩnh) |
| Auth | ASP.NET Core Identity, multi-tenant aware (user thuộc về 1 Tenant + PlatformSuperAdmin không thuộc tenant nào) |
| Mã hóa | AES cho credential (`client_secret`, `hmac_secret`) lưu trong DB + dữ liệu cá nhân nhạy cảm (CCCD, SĐT nhân sự) |
| Log | Serilog + bảng `SystemLog` có `TenantId`, lưu ≥ 2 năm |

**Cấu trúc solution:**
```
HanoiCheckPlatform.sln
 ├─ src/HCP.Web             → Blazor Server (2 khu vực: Platform admin / Tenant portal)
 ├─ src/HCP.Domain           → Entity (có TenantId), Enum
 ├─ src/HCP.Infrastructure   → EF Core, MultiTenant store, HanoiCheckApiClient, Hangfire jobs
 └─ tests/HCP.Tests          → unit test: HMAC signer, tenant isolation (query filter), mapping DTO
```

---

## 3. Mô hình dữ liệu

### 3.1. Bảng hạ tầng multi-tenant (MỚI, làm trước tiên)
| Bảng | Nội dung |
|---|---|
| `Tenant` | Id, Ten, MaSoThue, DiaChi, NguoiDaiDien, Email, SDT, TrangThai (PendingApproval/Active/Suspended), NgayDangKy, NgayDuyet |
| `TenantHnCCredential` | TenantId, ClientId, ClientSecret (mã hóa), HmacSecret (mã hóa), BaseUrl (sandbox/production), DaXacThuc (bool) |
| `TenantOAuthToken` | TenantId, AccessToken, RefreshToken, ExpiresAt — **1 dòng / tenant** (khác plan cũ: trước đây chỉ 1 dòng toàn hệ thống) |
| `ApplicationUser` | Identity user + `TenantId` (null nếu là PlatformSuperAdmin), Role |
| `SyncOutbox` | **+ cột `TenantId`**, EntityType, EntityKey, PayloadJson, Status, Attempts, LastError, LastHttpCode, NextRetryAt |
| `SystemLog` | **+ cột `TenantId`**, ai làm gì, request/response gửi HnC |

### 3.2. Bảng nghiệp vụ (giống spec, mỗi bảng **thêm cột `TenantId`** + global query filter)
| # | Entity | Endpoint đích | Xử lý |
|---|---|---|---|
| 1 | `Warehouse` (Kho) | `warehouses/merge` | Sync |
| 2 | `Facility` (Cơ sở) | `facilities/merge` | Async |
| 3 | `ProductionStep` (Khâu) | `steps/merge` | Sync |
| 4 | `ProductionProcess` + `ProcessStepLine` | `processes/merge` | Sync |
| 5 | `Staff` (Nhân sự) + `HealthCert`, `FoodSafetyCert` | `users/merge` | Sync |
| 6 | `SubSupplier` (NCC đầu vào) + `FoodGroup[]`, `AttpCert`, `Contract` | `sub-suppliers/merge` | Sync |
| 7 | `Product` (Thực phẩm/SKU) | `foods/merge` | Async |
| 8 | `Batch` (Lô SX) + `BatchWarehouse[]`, `BatchStep[]`, `BatchFile[]` | `batches/merge` | Async |
| 9 | `Dish` (Món ăn) + `Recipe[]`, `DishStep[]`, `DishFile[]` | `dishes/merge` | Async |
| 10 | `Order` (Đơn hàng) + `OrderLine[]`, `OrderImage[]`, `OrderExport[]` | `orders/merge` | Async |
| 11 | `StandardFoodCategory` | `GET standard-foods` | Read cache — **dùng chung mọi tenant** (không cần TenantId, gọi 1 lần bằng credential bất kỳ hoặc lưu global) |

> Lô sản xuất (Batch) và Món ăn (Dish) vẫn là 2 entity phức tạp nhất — giữ nguyên đề xuất UI wizard 4 bước từ plan trước, không đổi.

---

## 4. Danh sách màn hình

### Khu vực công khai (chưa đăng nhập)
1. Trang chủ giới thiệu + **Đăng ký cơ sở sản xuất**.
2. Trạng thái hồ sơ đăng ký (tra cứu bằng mã hồ sơ/email).

### Khu vực Platform Admin (`/admin`, chỉ PlatformSuperAdmin)
3. **Duyệt đăng ký tenant mới** (xem hồ sơ, Duyệt/Từ chối).
4. **Danh sách tenant** — trạng thái kết nối HnC, khóa/mở tenant.
5. **Dashboard giám sát đồng bộ toàn hệ thống** — tenant nào đang lỗi nhiều, cảnh báo.
6. **Nhật ký toàn hệ thống** (lọc theo tenant).

### Khu vực Tenant Portal (`/app`, TenantAdmin/TenantStaff — chỉ thấy dữ liệu tenant mình)
7. Dashboard riêng của cơ sở (tình trạng đồng bộ, giấy tờ sắp hết hạn).
8. **Cài đặt kết nối HanoiCheck** (nhập client_id/secret/hmac_secret, nút "Kiểm tra kết nối").
9. Quản lý người dùng nội bộ cơ sở (mời thêm nhân viên, phân quyền theo phân hệ).
10. Danh mục thực phẩm chuẩn (chỉ xem, đồng bộ tự động từ HnC).
11. Quản lý Kho / Cơ sở / Khâu sản xuất / Quy trình sản xuất.
12. Quản lý Nhân sự (kèm giấy khám sức khỏe, chứng nhận ATTP, cảnh báo hết hạn).
13. Quản lý NCC đầu vào.
14. Quản lý Thực phẩm/SKU.
15. Quản lý Lô sản xuất (wizard).
16. Quản lý Món ăn/Công thức.
17. Quản lý Đơn hàng + Xuất kho.
18. **Nhật ký đồng bộ của cơ sở** (xem payload, lỗi 422, trạng thái — không cần nút gửi thủ công vì tự động, nhưng có nút "Gửi lại" cho bản ghi lỗi).

---

## 5. Engine đồng bộ (per-tenant)

Về cơ bản giữ nguyên thiết kế outbox/Hangfire ở plan trước, chỉ khác:
1. **TokenManager theo tenant**: mỗi lần cần gọi API, tra `TenantOAuthToken` theo `TenantId`, hết hạn thì refresh bằng credential của đúng tenant đó.
2. **HmacSigner dùng `hmac_secret` của tenant hiện tại** khi ký canonical string — tuyệt đối không dùng nhầm secret giữa các tenant (viết unit test riêng để đảm bảo điều này).
3. **Hangfire job chạy theo lô nhưng nhóm theo TenantId** trước khi gọi API (vì mỗi tenant có base_url/token/secret khác nhau, không thể gộp request của nhiều tenant).
4. Tenant **chưa cấu hình xong credential** → job tự bỏ qua, đánh dấu outbox ở trạng thái `AwaitingCredential`, không tính là lỗi.
5. Toàn bộ vẫn tự động, đúng yêu cầu bạn chọn: cơ sở lưu dữ liệu → outbox ghi nhận → job nền tự đẩy, không cần thao tác thêm.

---

## 6. Lộ trình triển khai (dev solo, ước tính ~15-16 tuần)

| Giai đoạn | Nội dung | Thời gian |
|---|---|---|
| **0. Nền tảng multi-tenant** | Scaffold solution, tích hợp Finbuckle.MultiTenant, Identity 2 tầng role, Global Query Filter theo TenantId, unit test cách ly dữ liệu | 1.5 tuần |
| **1. Đăng ký & duyệt tenant** | Trang đăng ký công khai, màn duyệt Platform Admin, luồng kích hoạt email, màn Cài đặt kết nối HnC (nhập + test credential) | 1.5 tuần |
| **2. Danh mục lõi (trong tenant)** | Kho, Cơ sở, Khâu, Quy trình, NCC đầu vào — CRUD có TenantId tự động | 2 tuần |
| **3. Engine đồng bộ per-tenant + Nhân sự** | TokenManager/HmacSigner theo tenant, SyncOutbox, Hangfire, job cache standard-foods, màn Nhân sự — **test end-to-end đồng bộ với ít nhất 2 tenant song song trên sandbox để chắc chắn không lẫn credential** | 2.5 tuần |
| **4. Sản phẩm + Lô sản xuất** | Thực phẩm/SKU, wizard Lô sản xuất | 3 tuần |
| **5. Món ăn** | Công thức + khâu + file (tái dùng component) | 1.5 tuần |
| **6. Đơn hàng & Xuất kho** | Order + OrderLine + xuất kho | 1.5 tuần |
| **7. Dashboard & hoàn thiện** | Dashboard Platform (toàn hệ thống) + Dashboard Tenant, nhật ký đồng bộ, mã hóa dữ liệu nhạy cảm, cảnh báo hết hạn giấy tờ | 1.5 tuần |
| **8. UAT & go-live** | Mời 1-2 cơ sở thật dùng thử (pilot tenant), kiểm thử thật với HnC sandbox, sửa lỗi, go production | 1.5 tuần |

**Khác biệt lớn nhất so với plan cũ:** phần multi-tenant (giai đoạn 0-1) phải làm **NGAY TỪ ĐẦU** vì nó là nền móng xuyên suốt mọi bảng dữ liệu — không thể "thêm sau" mà không phải sửa lại toàn bộ query.

---

## 7. Rủi ro cần lưu ý (bổ sung so với plan cũ)

- **Rò rỉ dữ liệu chéo tenant** là rủi ro nghiêm trọng nhất của kiến trúc multi-tenant shared-DB — bắt buộc có unit test tự động kiểm tra global query filter hoạt động đúng trên MỌI entity mới thêm sau này (dễ quên khi code nhanh).
- **Nhầm credential giữa các tenant khi ký HMAC/gọi token** → hậu quả là đẩy dữ liệu tenant A bằng token tenant B (bị HnC từ chối hoặc tệ hơn là sai dữ liệu) — cần test riêng với ≥2 tenant giả lập chạy song song trước khi go-live.
- Tenant tự nhập credential sai (gõ nhầm secret) → cần nút "Kiểm tra kết nối" rõ ràng + thông báo lỗi cụ thể (401 với `error_code` như spec mục 1.3) ngay tại màn cài đặt, không để họ phát hiện qua nhật ký lỗi hàng loạt sau này.
- Quy trình đăng ký với HnC (5 bước, ngoài hệ thống bạn) là việc **từng tenant tự làm** — hệ thống chỉ hỗ trợ họ nhập credential sau khi có, không thay họ làm hồ sơ; nên có hướng dẫn/checklist ngay trong màn Cài đặt kết nối để giảm hỗ trợ thủ công.
- Việc duyệt tenant thủ công (bạn tự duyệt) sẽ tốn thời gian vận hành khi số lượng cơ sở tăng — cân nhắc thêm tiêu chí tự động duyệt sơ bộ (vd kiểm tra mã số thuế hợp lệ) ở giai đoạn sau.

---

## 8. Việc tiếp theo đề xuất

1. Xác nhận lại mô hình multi-tenant ở mục 1 đã đúng ý (đặc biệt: 1 Tenant = 1 NCC = 1 credential, có thể có nhiều "Cơ sở" con bên trong).
2. Khởi tạo git repo + solution .NET 8 với Finbuckle.MultiTenant.
3. Bắt đầu Giai đoạn 0: scaffold multi-tenant foundation trước tiên.
