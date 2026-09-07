using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="ILenhSanXuatService"/>
public sealed class LenhSanXuatService : ILenhSanXuatService
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public LenhSanXuatService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<IReadOnlyList<LenhSanXuat>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.LenhSanXuats.AsNoTracking()
            .OrderByDescending(l => l.NgaySanXuat).ThenByDescending(l => l.Id)
            .ToListAsync(ct);

    public Task<LenhSanXuat?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.LenhSanXuats.Include(l => l.TieuHao).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<NguyenLieuCanDto>> TinhNguyenLieuCanAsync(
        string maThanhPham, decimal soLuong, string maKho, CancellationToken ct = default)
    {
        var tp = await _db.Products
            .Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == maThanhPham, ct);
        if (tp is null || tp.DanhSachDinhMuc.Count == 0) return Array.Empty<NguyenLieuCanDto>();

        var sanPham = await _db.Products.AsNoTracking().ToListAsync(ct);
        var tenTheoMa = sanPham.GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First());

        var ket = new List<NguyenLieuCanDto>();
        foreach (var dm in tp.DanhSachDinhMuc)
        {
            var can = dm.SoLuong * soLuong;
            var ton = await TonAsync(dm.MaNguyenLieu, maKho, ct);
            var p = tenTheoMa.GetValueOrDefault(dm.MaNguyenLieu);
            ket.Add(new NguyenLieuCanDto(dm.MaNguyenLieu, p?.TenSanPham ?? dm.MaNguyenLieu,
                p?.DonViTinh, can, ton, ton >= can));
        }
        return ket;
    }

    public async Task<KetQuaThaoTac> TaoAsync(LenhSanXuat lenh, CancellationToken ct = default)
    {
        lenh.MaLenh = lenh.MaLenh?.Trim() ?? "";
        lenh.MaThanhPham = lenh.MaThanhPham?.Trim() ?? "";
        lenh.MaKho = lenh.MaKho?.Trim() ?? "";
        lenh.MaLoThanhPham = lenh.MaLoThanhPham?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(lenh.MaLenh)) return KetQuaThaoTac.Loi("Vui lòng nhập mã lệnh.");
        if (await _db.LenhSanXuats.AnyAsync(l => l.MaLenh == lenh.MaLenh, ct))
            return KetQuaThaoTac.Loi($"Mã lệnh \"{lenh.MaLenh}\" đã tồn tại.");
        if (lenh.SoLuong <= 0) return KetQuaThaoTac.Loi("Số lượng sản xuất phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(lenh.MaLoThanhPham)) return KetQuaThaoTac.Loi("Vui lòng nhập mã lô thành phẩm.");

        var tp = await _db.Products.Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == lenh.MaThanhPham, ct);
        if (tp is null || tp.LoaiSanPham != LoaiSanPham.ThanhPham)
            return KetQuaThaoTac.Loi("Vui lòng chọn thành phẩm hợp lệ.");
        if (tp.DanhSachDinhMuc.Count == 0)
            return KetQuaThaoTac.Loi($"\"{tp.TenSanPham}\" chưa có định mức. Khai định mức trước khi sản xuất.");
        if (!await _db.Warehouses.AnyAsync(k => k.MaKho == lenh.MaKho, ct))
            return KetQuaThaoTac.Loi("Vui lòng chọn kho hợp lệ.");

        lenh.TrangThai = TrangThaiLenhSX.MoiTao;
        lenh.GhiChu = lenh.GhiChu?.Trim();
        _db.LenhSanXuats.Add(lenh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã tạo lệnh sản xuất \"{lenh.MaLenh}\". Bấm \"Thực hiện\" để trừ nguyên liệu.");
    }

    public async Task<KetQuaThaoTac> ThucHienAsync(int id, CancellationToken ct = default)
    {
        var lenh = await _db.LenhSanXuats.Include(l => l.TieuHao).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.HoanThanh)
            return KetQuaThaoTac.Loi("Lệnh này đã thực hiện rồi.");

        var tp = await _db.Products.Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == lenh.MaThanhPham, ct);
        if (tp is null || tp.DanhSachDinhMuc.Count == 0)
            return KetQuaThaoTac.Loi("Thành phẩm không còn định mức, không thể thực hiện.");

        // Tính nhu cầu + gom lô FEFO cho từng nguyên liệu, kiểm tra đủ TRƯỚC khi trừ.
        var thieu = new List<string>();
        var keHoachTru = new List<(string MaNL, string MaLo, DateOnly? Hsd, decimal SoLuong)>();

        foreach (var dm in tp.DanhSachDinhMuc)
        {
            var can = dm.SoLuong * lenh.SoLuong;
            var lots = await LayLoFefoAsync(dm.MaNguyenLieu, lenh.MaKho, ct);
            var tongTon = lots.Sum(x => x.Ton);
            if (tongTon < can)
            {
                thieu.Add($"{dm.MaNguyenLieu} (cần {can:0.###}, còn {tongTon:0.###})");
                continue;
            }

            var conCanTru = can;
            foreach (var lot in lots)
            {
                if (conCanTru <= 0) break;
                var tru = Math.Min(conCanTru, lot.Ton);
                keHoachTru.Add((dm.MaNguyenLieu, lot.MaLo, lot.Hsd, tru));
                conCanTru -= tru;
            }
        }

        if (thieu.Count > 0)
            return KetQuaThaoTac.Loi("Không đủ nguyên liệu: " + string.Join("; ", thieu));

        var now = DateTime.UtcNow;

        // Trừ nguyên liệu (âm) theo kế hoạch FEFO + ghi tiêu hao.
        foreach (var t in keHoachTru)
        {
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = t.MaNL, MaKho = lenh.MaKho, MaLo = t.MaLo,
                SoLuong = -t.SoLuong, HanSuDung = t.Hsd,
                Loai = LoaiGiaoDichKho.XuatSanXuat, ChungTu = lenh.MaLenh, ThoiGianUtc = now
            });
            lenh.TieuHao.Add(new LenhSanXuatTieuHao
            {
                MaNguyenLieu = t.MaNL, MaLo = t.MaLo, SoLuong = t.SoLuong
            });
        }

        // Nhập thành phẩm (dương).
        _db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = lenh.MaThanhPham, MaKho = lenh.MaKho, MaLo = lenh.MaLoThanhPham,
            SoLuong = lenh.SoLuong, HanSuDung = lenh.HanSuDungThanhPham,
            Loai = LoaiGiaoDichKho.NhapThanhPham, ChungTu = lenh.MaLenh, ThoiGianUtc = now
        });

        lenh.TrangThai = TrangThaiLenhSX.HoanThanh;
        lenh.ThoiGianHoanThanhUtc = now;

        // Tuỳ chọn: sinh Lô sản xuất (Batch) cho thành phẩm để đồng bộ HanoiCheck, kèm truy xuất
        // lô nguyên liệu → lô thành phẩm. Batch được thêm cùng transaction; đẩy hàng đợi sau khi lưu.
        Batch? batch = null;
        if (lenh.TaoLoDongBo)
        {
            batch = await DungBatchTuLenhAsync(lenh, tp, keHoachTru, now, ct);
            if (batch is not null)
            {
                _db.Batches.Add(batch);
                lenh.MaLoDaTao = batch.MaLo;
            }
        }

        await _db.SaveChangesAsync(ct);

        if (batch is not null)
            await _outbox.ThemAsync("Batch", batch.MaLo, HnCPayloadMapper.LoSanXuat(batch), ct);

        var thongBao = $"Đã sản xuất {lenh.SoLuong:0.###} \"{tp.TenSanPham}\" (lô {lenh.MaLoThanhPham}), trừ nguyên liệu theo định mức.";
        if (batch is not null)
            thongBao += $" Đã tạo lô \"{batch.MaLo}\" và đưa vào hàng đợi đồng bộ HanoiCheck.";
        else if (lenh.TaoLoDongBo)
            thongBao += " (Lô đồng bộ đã tồn tại nên bỏ qua tạo mới.)";
        return KetQuaThaoTac.Ok(thongBao);
    }

    /// <summary>
    /// Dựng Batch từ một lệnh sản xuất đã tính kế hoạch tiêu hao: lô thành phẩm + kho + (nếu có
    /// khâu) một bước cho mỗi lô nguyên liệu tiêu hao (ma_lo_nguyen_lieu → ma_lo_san_xuat). Trả về
    /// null nếu mã lô này đã có Batch (tránh trùng khoá nghiệp vụ).
    /// </summary>
    private async Task<Batch?> DungBatchTuLenhAsync(
        LenhSanXuat lenh, Product tp,
        List<(string MaNL, string MaLo, DateOnly? Hsd, decimal SoLuong)> keHoachTru,
        DateTime now, CancellationToken ct)
    {
        if (await _db.Batches.AnyAsync(b => b.MaLo == lenh.MaLoThanhPham, ct)) return null;

        // Chọn khâu để gắn bước truy xuất: ưu tiên khâu đầu của quy trình thành phẩm, sau đó là
        // khâu bất kỳ trong danh mục. Không có khâu nào thì để danh sách bước rỗng (vẫn hợp lệ).
        string? maKhau = null;
        if (!string.IsNullOrWhiteSpace(tp.MaQuyTrinh))
        {
            maKhau = await _db.ProcessStepLines
                .Where(l => l.ProductionProcess!.MaQuyTrinh == tp.MaQuyTrinh)
                .OrderBy(l => l.ThuTu).Select(l => l.MaKhau).FirstOrDefaultAsync(ct);
        }
        maKhau ??= await _db.ProductionSteps.OrderBy(s => s.MaKhau)
            .Select(s => s.MaKhau).FirstOrDefaultAsync(ct);

        var moTaNL = string.Join("; ", keHoachTru.Select(t => $"{t.MaLo}×{t.SoLuong:0.###}"));

        var batch = new Batch
        {
            MaSanPham = lenh.MaThanhPham,
            MaLo = lenh.MaLoThanhPham,
            TenLo = $"Lô SX {lenh.MaLenh}",
            NgayNhap = lenh.NgaySanXuat,
            NgaySanXuat = lenh.NgaySanXuat,
            HanSuDung = lenh.HanSuDungThanhPham,
            GhiChu = $"Tự động từ lệnh sản xuất {lenh.MaLenh}."
                     + (moTaNL.Length > 0 ? $" Nguyên liệu tiêu hao: {moTaNL}." : ""),
            DanhSachKho = new() { new BatchWarehouse { MaKho = lenh.MaKho } }
        };

        if (!string.IsNullOrWhiteSpace(maKhau))
        {
            var thuTu = 1;
            foreach (var t in keHoachTru)
            {
                batch.DanhSachKhau.Add(new BatchStep
                {
                    MaBuocSx = $"{lenh.MaLenh}-{thuTu}",
                    MaKhau = maKhau,
                    ThuTu = thuTu,
                    ThoiGian = now,
                    MaLoNguyenLieu = t.MaLo,
                    MaLoSanXuat = lenh.MaLoThanhPham,
                    GhiChu = $"Tiêu hao {t.MaNL} lô {t.MaLo}: {t.SoLuong:0.###}"
                });
                thuTu++;
            }
        }

        return batch;
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var lenh = await _db.LenhSanXuats.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.HoanThanh)
            return KetQuaThaoTac.Loi("Không xoá được lệnh đã thực hiện (đã phát sinh giao dịch kho).");

        _db.LenhSanXuats.Remove(lenh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá lệnh \"{lenh.MaLenh}\".");
    }

    /// <summary>Tồn hiện tại của một nguyên liệu trong một kho (tổng mọi lô).</summary>
    private async Task<decimal> TonAsync(string maSanPham, string maKho, CancellationToken ct) =>
        await _db.KhoGiaoDichs
            .Where(g => g.MaSanPham == maSanPham && g.MaKho == maKho)
            .SumAsync(g => (decimal?)g.SoLuong, ct) ?? 0m;

    /// <summary>Các lô còn tồn của (sản phẩm, kho), sắp theo FEFO: hết hạn sớm trước, lô không HSD sau cùng.</summary>
    private async Task<List<(string MaLo, DateOnly? Hsd, decimal Ton)>> LayLoFefoAsync(
        string maSanPham, string maKho, CancellationToken ct)
    {
        var gd = await _db.KhoGiaoDichs
            .Where(g => g.MaSanPham == maSanPham && g.MaKho == maKho)
            .ToListAsync(ct);

        return gd
            .GroupBy(g => g.MaLo)
            .Select(g => (
                MaLo: g.Key,
                Hsd: g.Where(x => x.HanSuDung.HasValue).Max(x => x.HanSuDung),
                Ton: g.Sum(x => x.SoLuong)))
            .Where(x => x.Ton > 0)
            .OrderBy(x => x.Hsd ?? DateOnly.MaxValue).ThenBy(x => x.MaLo)
            .ToList();
    }
}
