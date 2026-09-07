/* ============================================================
   Thêm 18 nhân sự BTN-011..BTN-028 (vai trò: Người chế biến)
   Chạy trên SQL Server của PRODUCTION (DB của quanly.tuannghiabakery.vn).
   - Tự lấy TenantId từ BTN-001 đã có sẵn (không cần biết GUID).
   - Idempotent: bỏ qua mã đã tồn tại (chạy lại nhiều lần vô hại).
   - LƯU Ý: chèn thẳng DB nên KHÔNG tự đẩy sang HanoiCheck (đồng bộ sau nếu cần).
   - HÃY BACKUP DB trước khi chạy.
   ============================================================ */
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;

DECLARE @tid nvarchar(64) = (SELECT TOP 1 TenantId FROM Staff WHERE MaNhanSu = 'BTN-001');
IF @tid IS NULL
BEGIN
    RAISERROR(N'Khong tim thay TenantId tu BTN-001. Kiem tra lai.', 16, 1);
    RETURN;
END;

-- Giá trị chung cho cả 18 người
DECLARE @ngayKham date        = '2026-04-17';
DECLARE @ngayHetHanKham date  = '2027-04-16';
DECLARE @noiKham nvarchar(255)= N'Trạm Y tế Phường Cửa Nam';
DECLARE @ngayCapTH date       = '2026-02-28';
DECLARE @coQuanCap nvarchar(255) = N'Cty Bánh Tuấn Nghĩa';

;WITH data(MaNhanSu, HoTen, Ksk, Th) AS (
    SELECT 'BTN-011', N'Lê Thị Hồng Minh',   'MK13/GKSKTYTCN', 'TH-13' UNION ALL
    SELECT 'BTN-012', N'Hoàng Hồng Hạnh',    'MK3/GKSKTYTCN',  'TH-3'  UNION ALL
    SELECT 'BTN-013', N'Nguyễn Hồng Duyên',  'MK9/GKSKTYTCN',  'TH-9'  UNION ALL
    SELECT 'BTN-014', N'Nguyễn Thị Thuý Linh','MK5/GKSKTYTCN', 'TH-5'  UNION ALL  -- <== KIỂM TRA LẠI TÊN (ảnh bị che)
    SELECT 'BTN-015', N'Nguyễn Thị Thu Hà',  'MK8/GKSKTYTCN',  'TH-8'  UNION ALL
    SELECT 'BTN-016', N'Phạm Thị Lan Hương', 'MK4/GKSKTYTCN',  'TH-4'  UNION ALL
    SELECT 'BTN-017', N'Dương Đức Mạnh',     'MK7/GKSKTYTCN',  'TH-7'  UNION ALL
    SELECT 'BTN-018', N'Lê Văn Nam',         'MK6/GKSKTYTCN',  'TH-6'  UNION ALL
    SELECT 'BTN-019', N'Lê Lan Phương',      'MK15/GKSKTYTCN', 'TH-15' UNION ALL
    SELECT 'BTN-020', N'Lê Thị Minh Nhâm',   'MK16/GKSKTYTCN', 'TH-16' UNION ALL
    SELECT 'BTN-021', N'Nguyễn Thị Nhung',   'MK14/GKSKTYTCN', 'TH-14' UNION ALL
    SELECT 'BTN-022', N'Mai Anh Đức',        'MK27/GKSKTYTCN', 'TH-27' UNION ALL
    SELECT 'BTN-023', N'Nguyễn Minh Tuyền',  'MK21/GKSKTYTCN', 'TH-21' UNION ALL
    SELECT 'BTN-024', N'Trần Văn B',   'MK23/GKSKTYTCN', 'TH-23' UNION ALL
    SELECT 'BTN-025', N'Nguyễn Văn Huy',     'MK26/GKSKTYTCN', 'TH-26' UNION ALL
    SELECT 'BTN-026', N'Nguyễn Văn A',        'MK24/GKSKTYTCN', 'TH-24' UNION ALL
    SELECT 'BTN-027', N'Mai Anh Dũng',       'MK29/GKSKTYTCN', 'TH-29' UNION ALL
    SELECT 'BTN-028', N'Trịnh Thúy Phương',  'MK28/GKSKTYTCN', 'TH-28'
)
INSERT INTO Staff
    (MaNhanSu, HoTen, CccdEncrypted, LaChuCoSo, LaNguoiCheBien, LaNguoiGiaoHang, TrangThai,
     KskSoGiay, KskNgayKham, KskNgayHetHan, KskNoiKham,
     AttpSoChungNhan, AttpNgayCap, AttpCoQuanCap,
     CreatedAtUtc, TenantId)
SELECT
    d.MaNhanSu, d.HoTen, N'', 0, 1, 0, 1,
    d.Ksk, @ngayKham, @ngayHetHanKham, @noiKham,
    d.Th, @ngayCapTH, @coQuanCap,
    GETUTCDATE(), @tid
FROM data d
WHERE NOT EXISTS (
    SELECT 1 FROM Staff s WHERE s.MaNhanSu = d.MaNhanSu AND s.TenantId = @tid
);

PRINT N'Da chen: ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' nhan su.';

-- Kiểm tra kết quả
SELECT MaNhanSu, HoTen, LaNguoiCheBien AS CheBien, KskSoGiay, KskNgayKham, KskNgayHetHan,
       AttpSoChungNhan, AttpNgayCap
FROM Staff
WHERE TenantId = @tid AND MaNhanSu BETWEEN 'BTN-011' AND 'BTN-028'
ORDER BY MaNhanSu;
