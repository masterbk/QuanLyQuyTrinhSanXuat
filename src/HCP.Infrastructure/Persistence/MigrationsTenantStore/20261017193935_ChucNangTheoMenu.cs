using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.MigrationsTenantStore
{
    /// <summary>
    /// Đổi phân quyền chức năng từ 6 "module" gộp sang mã theo TỪNG MENU (xem HCP.Domain.Constants.ChucNang).
    /// Chỉ đổi DỮ LIỆU trong Tenants.ChucNangCsv, không đổi cấu trúc bảng.
    ///
    /// Quy đổi: nhóm danh mục trước đây luôn hiện nên cơ sở nào cũng được mở; còn lại tách theo module cũ.
    /// </summary>
    public partial class ChucNangTheoMenu : Migration
    {
        private const string CoBan = "Kho,CoSoSanXuat,KhauSanXuat,QuyTrinh,NccDauVao,NhanSu,ThucPham";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE Tenants
   SET ChucNangCsv = '{CoBan}'
     + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,LoSanXuat,%'    THEN ',LoSanXuat' ELSE '' END
     + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,SuatAn,%'       THEN ',MonAn,ThucDon' ELSE '' END
     + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,KhoSanXuat,%'   THEN ',TonKho,DinhMuc,LenhSanXuat' ELSE '' END
     + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,BanHang,%'      THEN ',KhachHang,DonHangBan' ELSE '' END
     + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,HanoiCheck,%'   THEN ',KetNoiHnC,NhatKyDongBo,DonHangNhan' ELSE '' END
     + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,KiemSoatAttp,%' THEN ',BieuMau,NhapPhieu,PhieuGhiNhan,NhacHan' ELSE '' END
 WHERE ChucNangCsv IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gộp ngược về 6 module cũ: có bất kỳ menu con nào thì coi như module đó đang bật.
            migrationBuilder.Sql(@"
UPDATE Tenants
   SET ChucNangCsv = STUFF(
         CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,LoSanXuat,%' THEN ',LoSanXuat' ELSE '' END
       + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,MonAn,%'
                or ',' + ChucNangCsv + ',' LIKE '%,ThucDon,%' THEN ',SuatAn' ELSE '' END
       + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,TonKho,%'
                or ',' + ChucNangCsv + ',' LIKE '%,DinhMuc,%'
                or ',' + ChucNangCsv + ',' LIKE '%,LenhSanXuat,%' THEN ',KhoSanXuat' ELSE '' END
       + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,DonHangBan,%'
                or ',' + ChucNangCsv + ',' LIKE '%,KhachHang,%' THEN ',BanHang' ELSE '' END
       + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,KetNoiHnC,%'
                or ',' + ChucNangCsv + ',' LIKE '%,NhatKyDongBo,%' THEN ',HanoiCheck' ELSE '' END
       + CASE WHEN ',' + ChucNangCsv + ',' LIKE '%,BieuMau,%'
                or ',' + ChucNangCsv + ',' LIKE '%,NhapPhieu,%' THEN ',KiemSoatAttp' ELSE '' END,
       1, 1, '')
 WHERE ChucNangCsv IS NOT NULL;");
        }
    }
}
