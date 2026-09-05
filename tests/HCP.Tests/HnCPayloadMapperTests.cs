using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng payload gửi HanoiCheck đúng định dạng đặc tả: tên trường snake_case,
/// bỏ hẳn trường không bắt buộc khi rỗng, ngày dạng ISO, và cấu trúc lồng nhau đúng.
/// Sai định dạng payload là lỗi âm thầm - HnC trả 422 chứ build vẫn xanh.
/// </summary>
public class HnCPayloadMapperTests
{
    /// <summary>Tuần tự hoá đúng bằng options dùng thật (bỏ null) rồi soi lại thành JsonElement.</summary>
    private static JsonElement Ser(object payload)
    {
        var json = JsonSerializer.Serialize(payload, HnCPayloadMapper.Json);
        return JsonDocument.Parse(json).RootElement[0]; // mảng 1 phần tử -> lấy phần tử đầu
    }

    [Fact]
    public void Kho_Dung_Field_Snake_Case()
    {
        var e = Ser(HnCPayloadMapper.Kho(new Warehouse
        {
            MaKho = "KHO01", TenKho = "Kho A", DiaChi = "Hà Nội", DienTich = 100.5m
        }));

        Assert.Equal("KHO01", e.GetProperty("ma_kho").GetString());
        Assert.Equal("Kho A", e.GetProperty("ten_kho").GetString());
        Assert.Equal("Hà Nội", e.GetProperty("dia_chi").GetString());
        Assert.Equal(100.5m, e.GetProperty("dien_tich").GetDecimal());
    }

    [Fact]
    public void Kho_Khong_Dien_Tich_Thi_Bo_Han_Field()
    {
        var e = Ser(HnCPayloadMapper.Kho(new Warehouse { MaKho = "KHO01", TenKho = "Kho A", DiaChi = "HN" }));
        Assert.False(e.TryGetProperty("dien_tich", out _)); // null -> bỏ hẳn, không gửi "dien_tich": null
    }

    [Fact]
    public void QuyTrinh_Co_Danh_Sach_Khau_Theo_Thu_Tu()
    {
        var p = new ProductionProcess
        {
            MaQuyTrinh = "QT001", TenQuyTrinh = "Chế biến thịt", MaDanhMucThucPham = 1,
            DanhSachKhau =
            {
                new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 2 },
                new ProcessStepLine { MaKhau = "GIET_MO", ThuTu = 1 }
            }
        };
        var e = Ser(HnCPayloadMapper.QuyTrinh(p));

        Assert.Equal("QT001", e.GetProperty("ma_quy_trinh").GetString());
        Assert.Equal(1, e.GetProperty("ma_danh_muc_thuc_pham").GetInt32());

        var khau = e.GetProperty("danh_sach_khau");
        Assert.Equal(2, khau.GetArrayLength());
        // Sắp theo thu_tu tăng dần bất kể thứ tự nhập.
        Assert.Equal("GIET_MO", khau[0].GetProperty("ma_khau").GetString());
        Assert.Equal(1, khau[0].GetProperty("thu_tu").GetInt32());
        Assert.Equal("SO_CHE", khau[1].GetProperty("ma_khau").GetString());
    }

    [Fact]
    public void QuyTrinh_Khong_Co_Ma_Danh_Muc_Thi_Bo_Han_Field()
    {
        // ma_danh_muc_thuc_pham chưa tra cứu được (điểm cần hỏi HnC) -> để null -> bỏ hẳn.
        var p = new ProductionProcess
        {
            MaQuyTrinh = "QT002", TenQuyTrinh = "X", MaDanhMucThucPham = null,
            DanhSachKhau = { new ProcessStepLine { MaKhau = "GIET_MO", ThuTu = 1 } }
        };
        var e = Ser(HnCPayloadMapper.QuyTrinh(p));
        Assert.False(e.TryGetProperty("ma_danh_muc_thuc_pham", out _));
    }

    [Fact]
    public void NccDauVao_Nhom_Thuc_Pham_La_Mang_Chuoi()
    {
        var s = new SubSupplier
        {
            MaNccDauVao = "NCC001", Ten = "Trại ABC",
            NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "THIT" }, new SubSupplierFoodGroup { MaNhom = "TRUNG" } }
        };
        var e = Ser(HnCPayloadMapper.NccDauVao(s));

        var nhom = e.GetProperty("nhom_thuc_pham");
        Assert.Equal(2, nhom.GetArrayLength());
        Assert.Equal("THIT", nhom[0].GetString());

        // Không khai giấy ATTP / hợp đồng -> bỏ hẳn 2 object đó.
        Assert.False(e.TryGetProperty("giay_chung_nhan_attp", out _));
        Assert.False(e.TryGetProperty("hop_dong", out _));
    }

    [Fact]
    public void LoSanXuat_Long_Kho_Khau_File_Dung_Dinh_Dang()
    {
        var b = new Batch
        {
            MaSanPham = "SP001", MaLo = "LO202607", TenLo = "Lô tháng 7",
            NgayNhap = new DateOnly(2026, 7, 20),
            HanSuDung = new DateOnly(2026, 7, 27),
            DanhSachKho = { new BatchWarehouse { MaKho = "KHO01" } },
            DanhSachKhau =
            {
                new BatchStep
                {
                    MaBuocSx = "BSX002", MaKhau = "SO_CHE", ThuTu = 2
                },
                new BatchStep
                {
                    MaBuocSx = "BSX001", MaKhau = "GIET_MO", ThuTu = 1,
                    ThoiGian = new DateTime(2026, 7, 19, 4, 30, 0),
                    TrangThai = "HOAN_THANH", NguoiThucHienCsv = "NV001,NV003"
                }
            },
            DanhSachFile =
            {
                new BatchFile { MaFile = "F001", TenFile = "Giấy kiểm dịch",
                                DuongDan = "uploads/kd.pdf", Loai = "GIAY_KIEM_DICH", MaKhau = "GIET_MO" }
            }
        };
        var e = Ser(HnCPayloadMapper.LoSanXuat(b));

        Assert.Equal("SP001", e.GetProperty("ma_san_pham").GetString());
        Assert.Equal("2026-07-20", e.GetProperty("ngay_nhap").GetString());
        Assert.False(e.TryGetProperty("ngay_san_xuat", out _)); // null -> bỏ

        // Kho: mảng object { ma_kho }.
        Assert.Equal("KHO01", e.GetProperty("danh_sach_kho")[0].GetProperty("ma_kho").GetString());

        // Khâu: sắp theo thu_tu tăng dần, thời gian dạng datetime, người thực hiện là mảng.
        var khau = e.GetProperty("danh_sach_khau");
        Assert.Equal("BSX001", khau[0].GetProperty("ma_buoc_sx").GetString());
        Assert.Equal("2026-07-19 04:30:00", khau[0].GetProperty("thoi_gian").GetString());
        var nguoi = khau[0].GetProperty("danh_sach_nguoi_thuc_hien");
        Assert.Equal(2, nguoi.GetArrayLength());
        Assert.Equal("NV001", nguoi[0].GetString());
        // Khâu thứ 2 không có người thực hiện -> bỏ hẳn field đó.
        Assert.False(khau[1].TryGetProperty("danh_sach_nguoi_thuc_hien", out _));

        // File.
        var file = e.GetProperty("danh_sach_file")[0];
        Assert.Equal("F001", file.GetProperty("ma_file").GetString());
        Assert.Equal("GIAY_KIEM_DICH", file.GetProperty("loai").GetString());
    }

    [Fact]
    public void LoSanXuat_Khong_Khau_Khong_File_Thi_Bo_Han_Mang()
    {
        var b = new Batch
        {
            MaSanPham = "SP001", MaLo = "LO01", TenLo = "Lô",
            NgayNhap = new DateOnly(2026, 7, 20),
            DanhSachKho = { new BatchWarehouse { MaKho = "KHO01" } }
        };
        var e = Ser(HnCPayloadMapper.LoSanXuat(b));

        Assert.True(e.TryGetProperty("danh_sach_kho", out _));       // bắt buộc, luôn có
        Assert.False(e.TryGetProperty("danh_sach_khau", out _));     // rỗng -> bỏ
        Assert.False(e.TryGetProperty("danh_sach_file", out _));     // rỗng -> bỏ
    }

    [Fact]
    public void MonAn_Cong_Thuc_Va_Khau_Dung_Dinh_Dang()
    {
        var d = new Dish
        {
            MaMonAn = "MON001", TenMonAn = "Thịt heo kho trứng", NhomTuoiId = 1,
            MoTa = "Món mặn",
            DanhSachNguyenLieu =
            {
                new DishIngredient { MaNguyenLieu = "SP001", DinhLuong = 0.06m, DonViTinhId = 3 },
                new DishIngredient { MaNguyenLieu = "SP002", DinhLuong = 0.05m } // không đơn vị
            },
            DanhSachKhau =
            {
                new DishStep { MaKhau = "SO_CHE", ThuTu = 1,
                               ThoiGian = new DateTime(2026, 7, 26, 5, 0, 0),
                               NguoiThucHienCsv = "NV001" }
            }
        };
        var e = Ser(HnCPayloadMapper.MonAn(d));

        Assert.Equal("MON001", e.GetProperty("ma_mon_an").GetString());
        Assert.Equal(1, e.GetProperty("nhom_tuoi_id").GetInt32());

        var nl = e.GetProperty("danh_sach_nguyen_lieu");
        Assert.Equal(2, nl.GetArrayLength());
        Assert.Equal("SP001", nl[0].GetProperty("ma_nguyen_lieu").GetString());
        Assert.Equal(3, nl[0].GetProperty("don_vi_tinh_id").GetInt32());
        Assert.False(nl[1].TryGetProperty("don_vi_tinh_id", out _)); // null -> bỏ

        var khau = e.GetProperty("danh_sach_khau")[0];
        Assert.Equal("SO_CHE", khau.GetProperty("ma_khau").GetString());
        Assert.Equal("2026-07-26 05:00:00", khau.GetProperty("thoi_gian").GetString());
        Assert.Equal("NV001", khau.GetProperty("danh_sach_nguoi_thuc_hien")[0].GetString());
    }

    [Fact]
    public void ThucPham_Dung_Field_Va_Bo_Field_Null()
    {
        var e = Ser(HnCPayloadMapper.ThucPham(new Product
        {
            MaSanPham = "SP001", TenSanPham = "Thịt heo ba chỉ", MaLoaiSp = "THIT",
            Gtin = "8938505974194", MaQuyTrinh = "QT001"
            // MaThucPhamChuan, QuocGia, MoTa để null
        }));

        Assert.Equal("SP001", e.GetProperty("ma_san_pham").GetString());
        Assert.Equal("Thịt heo ba chỉ", e.GetProperty("ten_san_pham").GetString());
        Assert.Equal("THIT", e.GetProperty("ma_loai_sp").GetString());
        Assert.Equal("8938505974194", e.GetProperty("gtin").GetString());
        Assert.Equal("QT001", e.GetProperty("ma_quy_trinh").GetString());
        // Các trường không khai bị bỏ hẳn.
        Assert.False(e.TryGetProperty("ma_thuc_pham_chuan", out _));
        Assert.False(e.TryGetProperty("quoc_gia", out _));
        Assert.False(e.TryGetProperty("mo_ta", out _));
    }

    [Fact]
    public void NhanSu_Field_Bat_Buoc_Va_Boolean_Vai_Tro()
    {
        var e = Ser(HnCPayloadMapper.NhanSu(new Staff
        {
            MaNhanSu = "NV001", HoTen = "Nguyễn Văn A", ViTri = "Quản lý",
            NgaySinh = new DateOnly(1990, 5, 12), Cccd = "079090012345",
            LaChuCoSo = true, LaNguoiCheBien = false, LaNguoiGiaoHang = false, TrangThai = true
        }));

        Assert.Equal("NV001", e.GetProperty("ma_nhan_su").GetString());
        Assert.Equal("Nguyễn Văn A", e.GetProperty("ho_ten").GetString());
        Assert.Equal("1990-05-12", e.GetProperty("ngay_sinh").GetString());
        Assert.Equal("079090012345", e.GetProperty("cccd").GetString());
        // Boolean bắt buộc luôn xuất hiện (kể cả false).
        Assert.True(e.GetProperty("la_chu_co_so").GetBoolean());
        Assert.False(e.GetProperty("la_nguoi_che_bien").GetBoolean());
        Assert.True(e.GetProperty("trang_thai").GetBoolean());
    }

    [Fact]
    public void NhanSu_Khong_Giay_To_Thi_Bo_Han_Object_Va_Cccd()
    {
        var e = Ser(HnCPayloadMapper.NhanSu(new Staff
        {
            MaNhanSu = "NV002", HoTen = "Trần B", TrangThai = true
        }));

        Assert.False(e.TryGetProperty("cccd", out _)); // không khai -> bỏ hẳn
        Assert.False(e.TryGetProperty("giay_kham_suc_khoe", out _));
        Assert.False(e.TryGetProperty("chung_nhan_tap_huan_attp", out _));
    }

    [Fact]
    public void NhanSu_Co_Giay_Kham_Suc_Khoe_Thi_Lam_Object_Long()
    {
        var e = Ser(HnCPayloadMapper.NhanSu(new Staff
        {
            MaNhanSu = "NV003", HoTen = "Lê C", TrangThai = true,
            KskSoGiay = "KSK-2026-001",
            KskNgayKham = new DateOnly(2026, 1, 10),
            KskNgayHetHan = new DateOnly(2027, 1, 10),
            KskNoiKham = "Bệnh viện Thanh Nhàn"
        }));

        var ksk = e.GetProperty("giay_kham_suc_khoe");
        Assert.Equal("KSK-2026-001", ksk.GetProperty("so_giay").GetString());
        Assert.Equal("2026-01-10", ksk.GetProperty("ngay_kham").GetString());
        Assert.Equal("2027-01-10", ksk.GetProperty("ngay_het_han").GetString());
        Assert.Equal("Bệnh viện Thanh Nhàn", ksk.GetProperty("noi_kham").GetString());
    }

    [Fact]
    public void NccDauVao_Co_Giay_ATTP_Thi_Ngay_Dinh_Dang_ISO()
    {
        var s = new SubSupplier
        {
            MaNccDauVao = "NCC002", Ten = "Trại XYZ",
            NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "RAU_CU" } },
            AttpSoGiay = "ATTP-2026-0099",
            AttpNgayCap = new DateOnly(2025, 6, 1),
            AttpNgayHetHan = new DateOnly(2028, 6, 1)
        };
        var e = Ser(HnCPayloadMapper.NccDauVao(s));

        var attp = e.GetProperty("giay_chung_nhan_attp");
        Assert.Equal("ATTP-2026-0099", attp.GetProperty("so_giay").GetString());
        Assert.Equal("2025-06-01", attp.GetProperty("ngay_cap").GetString());
        Assert.Equal("2028-06-01", attp.GetProperty("ngay_het_han").GetString());
    }
}
