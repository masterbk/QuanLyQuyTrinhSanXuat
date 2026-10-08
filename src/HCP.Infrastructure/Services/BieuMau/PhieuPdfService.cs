using HCP.Domain;
using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;
using HCP.Infrastructure.Services.DanhMuc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HCP.Infrastructure.Services.BieuMau;

/// <summary>Dựng PDF một phiếu ghi nhận theo đúng dáng biểu mẫu giấy (header, bảng, ô ký thẩm tra).</summary>
public interface IPhieuPdfService
{
    /// <summary>Trả về PDF của phiếu, hoặc null nếu không tìm thấy.</summary>
    Task<byte[]?> TaoPdfAsync(int phieuId, CancellationToken ct = default);

    /// <summary>
    /// PDF báo cáo tháng của một biểu mẫu. Checklist có trường Đạt/Không đạt: ma trận hạng mục × ngày + bảng ghi chú;
    /// mẫu khác: bảng phẳng mỗi dòng phiếu một hàng (Ngày + trường đầu phiếu + trường dòng). Null nếu không có mẫu.
    /// </summary>
    Task<byte[]?> TaoBaoCaoThangAsync(int bieuMauId, int nam, int thang, CancellationToken ct = default);
}

public sealed class PhieuPdfService : IPhieuPdfService
{
    private readonly IPhieuGhiNhanService _phieu;
    private readonly AppDbContext _db;
    private readonly IDanhMucService<Staff> _nhanSu;
    private readonly IDanhMucService<Product> _sanPham;
    private readonly IDanhMucService<SubSupplier> _ncc;
    private readonly IDanhMucService<Facility> _coSo;
    private readonly IDocAnhPhieu? _docAnh;
    private readonly string? _gocWeb;   // Uploads:BaseUrl - gốc đường dẫn trang tra cứu công khai cho mã QR

    public PhieuPdfService(IPhieuGhiNhanService phieu, AppDbContext db,
                           IDanhMucService<Staff> nhanSu, IDanhMucService<Product> sanPham,
                           IDanhMucService<SubSupplier> ncc, IDanhMucService<Facility> coSo,
                           IDocAnhPhieu? docAnh = null,
                           Microsoft.Extensions.Configuration.IConfiguration? cauHinh = null)
    {
        _gocWeb = cauHinh?["Uploads:BaseUrl"]?.Trim().TrimEnd('/');
        _phieu = phieu;
        _db = db;
        _nhanSu = nhanSu;
        _sanPham = sanPham;
        _ncc = ncc;
        _coSo = coSo;
        _docAnh = docAnh;
    }

    public async Task<byte[]?> TaoPdfAsync(int phieuId, CancellationToken ct = default)
    {
        var phieu = await _phieu.LayPhieuTheoIdAsync(phieuId, ct);
        if (phieu is null) return null;
        var mau = await _phieu.LayBieuMauAsync(phieu.BieuMauId, ct);
        if (mau is null) return null;

        var nhanSu = (await _nhanSu.LayTatCaAsync(ct)).GroupBy(n => n.MaNhanSu).ToDictionary(g => g.Key, g => g.First().HoTen);
        var sanPham = (await _sanPham.LayTatCaAsync(ct)).GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First().TenSanPham);
        var ncc = (await _ncc.LayTatCaAsync(ct)).GroupBy(n => n.MaNccDauVao).ToDictionary(g => g.Key, g => g.First().Ten);
        var coSo = (await _coSo.LayTatCaAsync(ct)).GroupBy(c => c.MaCoSo).ToDictionary(g => g.Key, g => g.First().TenCoSo);
        var thongTinIn = await LayThongTinInAsync(ct);

        string Resolve(TruongBieuMau t, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return t.Kieu switch
            {
                KieuTruongBieuMau.ChonNhanSu => nhanSu.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.ChonSanPham => sanPham.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.ChonNcc => ncc.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.ChonCoSo => coSo.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.Ngay => DateOnly.TryParse(raw, out var d) ? d.ToString("dd/MM/yyyy") : raw,
                KieuTruongBieuMau.Anh => "Có ảnh",
                _ => raw
            };
        }

        var truongDau = mau.Truong.Where(t => t.LaDauPhieu).OrderBy(t => t.ThuTu).ToList();
        var truong = mau.Truong.Where(t => !t.LaDauPhieu).OrderBy(t => t.ThuTu).ToList();
        var giaTriDau = DocGiaTri(phieu.GiaTriDauJson);
        var hangMucTen = mau.HangMuc.ToDictionary(h => h.Id, h => h.Ten);
        var laChecklist = mau.BoCuc == BoCucBieuMau.Checklist;

        // Gom ảnh (trường kiểu Ảnh) để nhúng ở cuối PDF - chỉ khi đọc được file.
        var dsAnh = new List<(string Caption, byte[] Bytes)>();
        if (_docAnh is not null)
        {
            foreach (var t in mau.Truong.Where(t => t.LaDauPhieu && t.Kieu == KieuTruongBieuMau.Anh))
            {
                var b = _docAnh.Doc(giaTriDau.GetValueOrDefault(t.Ma));
                if (b is not null) dsAnh.Add((t.Ten, b));
            }
            var anhDong = mau.Truong.Where(t => !t.LaDauPhieu && t.Kieu == KieuTruongBieuMau.Anh).ToList();
            if (anhDong.Count > 0)
            {
                var sttAnh = 1;
                foreach (var dong in phieu.Dong.OrderBy(d => d.ThuTu))
                {
                    var gt = DocGiaTri(dong.GiaTriJson);
                    foreach (var t in anhDong)
                    {
                        var b = _docAnh.Doc(gt.GetValueOrDefault(t.Ma));
                        if (b is not null) dsAnh.Add(($"Dòng {sttAnh} - {t.Ten}", b));
                    }
                    sttAnh++;
                }
            }
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Element(c => KhungHeader(c, mau, thongTinIn));
                    col.Item().PaddingTop(6).AlignCenter().Text(mau.Ten.ToUpper()).Bold().FontSize(14);
                    col.Item().PaddingTop(2).Text(t =>
                    {
                        t.Span("Ngày: ").SemiBold();
                        t.Span($"{phieu.Ngay:dd/MM/yyyy}   ");
                        foreach (var td in truongDau)
                        {
                            var v = Resolve(td, giaTriDau.GetValueOrDefault(td.Ma));
                            if (string.IsNullOrWhiteSpace(v)) continue;
                            t.Span($"{td.Ten}: ").SemiBold();
                            t.Span($"{v}   ");
                        }
                        var tenLap = phieu.TenNguoiLap
                                     ?? (phieu.NguoiLap is null ? null : nhanSu.GetValueOrDefault(phieu.NguoiLap, phieu.NguoiLap));
                        if (!string.IsNullOrWhiteSpace(tenLap))
                        {
                            t.Span("Người lập: ").SemiBold();
                            t.Span(tenLap);
                        }
                    });
                    col.Item().PaddingTop(4);
                });

                page.Content().Column(noiDung =>
                {
                    noiDung.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(28);                 // STT
                            if (laChecklist) cols.RelativeColumn(3); // Hạng mục
                            foreach (var _ in truong) cols.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(Tieu).Text("STT");
                            if (laChecklist) h.Cell().Element(Tieu).Text("Hạng mục");
                            foreach (var t in truong) h.Cell().Element(Tieu).Text(TieuDeCot(t));
                        });

                        var stt = 1;
                        foreach (var dong in phieu.Dong.OrderBy(d => d.ThuTu))
                        {
                            var giaTri = DocGiaTri(dong.GiaTriJson);
                            table.Cell().Element(O).Text(stt++.ToString());
                            if (laChecklist)
                                table.Cell().Element(O).Text(dong.HangMucBieuMauId is { } hm ? hangMucTen.GetValueOrDefault(hm, "") : "");
                            foreach (var t in truong)
                                table.Cell().Element(O).Text(Resolve(t, giaTri.GetValueOrDefault(t.Ma)));
                        }
                    });

                    // Ảnh đính kèm (trường kiểu Ảnh) - mỗi hàng tối đa 3 ảnh.
                    if (dsAnh.Count > 0)
                    {
                        noiDung.Item().PaddingTop(8).Text("Ảnh đính kèm").Bold().FontSize(10);
                        foreach (var hang in dsAnh.Chunk(3))
                        {
                            noiDung.Item().PaddingTop(4).Row(r =>
                            {
                                foreach (var (caption, bytes) in hang)
                                    r.RelativeItem().Padding(2).Column(ic =>
                                    {
                                        ic.Item().Text(caption).FontSize(8).SemiBold();
                                        ic.Item().PaddingTop(2).Height(120).Image(bytes).FitArea();
                                    });
                                for (var k = hang.Length; k < 3; k++) r.RelativeItem();
                            });
                        }
                    }

                    // Khối ký + mã QR đối chiếu bản gốc (phiếu đã hoàn thành có chữ ký).
                    noiDung.Item().PaddingTop(10).Element(c => KhoiKy(c, phieu));
                });

                page.Footer().Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(mau.GhiChuChan))
                        col.Item().PaddingTop(4).Text(mau.GhiChuChan).Italic().FontSize(8);
                    col.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Text("");
                        r.ConstantItem(300).Text("QC thẩm tra: ………………  Ngày: …………  [ ] Đạt   [ ] Không đạt").FontSize(9);
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    public async Task<byte[]?> TaoBaoCaoThangAsync(int bieuMauId, int nam, int thang, CancellationToken ct = default)
    {
        var mau = await _phieu.LayBieuMauAsync(bieuMauId, ct);
        if (mau is null) return null;
        var dsPhieu = await _phieu.LayPhieuThangAsync(bieuMauId, nam, thang, ct);

        var nhanSu = (await _nhanSu.LayTatCaAsync(ct)).GroupBy(n => n.MaNhanSu).ToDictionary(g => g.Key, g => g.First().HoTen);
        var sanPham = (await _sanPham.LayTatCaAsync(ct)).GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First().TenSanPham);
        var ncc = (await _ncc.LayTatCaAsync(ct)).GroupBy(n => n.MaNccDauVao).ToDictionary(g => g.Key, g => g.First().Ten);
        var coSo = (await _coSo.LayTatCaAsync(ct)).GroupBy(c => c.MaCoSo).ToDictionary(g => g.Key, g => g.First().TenCoSo);
        var thongTinIn = await LayThongTinInAsync(ct);

        string Resolve(TruongBieuMau t, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return t.Kieu switch
            {
                KieuTruongBieuMau.ChonNhanSu => nhanSu.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.ChonSanPham => sanPham.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.ChonNcc => ncc.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.ChonCoSo => coSo.GetValueOrDefault(raw, raw),
                KieuTruongBieuMau.Ngay => DateOnly.TryParse(raw, out var d) ? d.ToString("dd/MM/yyyy") : raw,
                KieuTruongBieuMau.Anh => "Có ảnh",
                _ => raw
            };
        }

        var truongDau = mau.Truong.Where(t => t.LaDauPhieu).OrderBy(t => t.ThuTu).ToList();
        var truong = mau.Truong.Where(t => !t.LaDauPhieu).OrderBy(t => t.ThuTu).ToList();
        var hangMucTen = mau.HangMuc.ToDictionary(h => h.Id, h => h.Ten);
        var laChecklist = mau.BoCuc == BoCucBieuMau.Checklist;
        // Checklist có trường Đạt/Không đạt -> ma trận hạng mục × ngày (mỗi trường Đạt/KĐ là 1 cột con của ngày).
        var cot = laChecklist ? truong.Where(t => t.Kieu == KieuTruongBieuMau.DatKhongDat).ToList() : new();

        void TieuDe(IContainer c) => c.Column(col =>
        {
            col.Item().Element(x => KhungHeader(x, mau, thongTinIn));
            col.Item().PaddingTop(6).AlignCenter().Text(mau.Ten.ToUpper()).Bold().FontSize(14);
            col.Item().AlignCenter().Text($"BÁO CÁO THÁNG {thang:00}/{nam}").SemiBold().FontSize(11);
            col.Item().PaddingTop(4);
        });

        void ChanTrang(IContainer c) => c.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(mau.GhiChuChan))
                col.Item().PaddingTop(4).Text(mau.GhiChuChan).Italic().FontSize(8);
            col.Item().PaddingTop(10).Row(r =>
            {
                r.RelativeItem().Text(x => { x.CurrentPageNumber(); x.Span("/"); x.TotalPages(); });
                r.ConstantItem(300).Text("QC thẩm tra: ………………  Ngày: …………  [ ] Đạt   [ ] Không đạt").FontSize(9);
            });
        });

        Document doc;
        if (cot.Count > 0)
        {
            var maTran = GomMaTranChecklist(dsPhieu, cot);
            var soNgay = DateTime.DaysInMonth(nam, thang);
            // Quá số cột con tối đa của 1 trang A4 ngang thì tách nửa tháng.
            var ngayMoiTrang = cot.Count * soNgay <= SoCotConToiDa ? soNgay : (soNgay + 1) / 2;
            var cacTrang = Enumerable.Range(1, soNgay).Chunk(ngayMoiTrang).ToList();
            var ghiChu = GomGhiChuChecklist(dsPhieu, truongDau, truong, hangMucTen, Resolve);
            var chuGiai = cot.Count > 1
                ? "Cột con mỗi ngày: " + string.Join(", ", cot.Select((t, k) => $"{k + 1} = {t.Ten}")) + ".  X = Đạt, O = Không đạt, trống = chưa kiểm."
                : $"{cot[0].Ten}: X = Đạt, O = Không đạt, trống = chưa kiểm.";

            doc = Document.Create(container =>
            {
                for (var i = 0; i < cacTrang.Count; i++)
                {
                    var ngays = cacTrang[i];
                    var trangCuoi = i == cacTrang.Count - 1;
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(7));
                        page.Header().Element(TieuDe);
                        page.Content().Column(nd =>
                        {
                            nd.Item().PaddingBottom(3).Text(chuGiai).FontSize(8);
                            nd.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(130);
                                    foreach (var _ in ngays)
                                        for (var k = 0; k < cot.Count; k++) cols.RelativeColumn();
                                });
                                table.Header(h =>
                                {
                                    h.Cell().RowSpan(cot.Count > 1 ? 2u : 1u).Element(TieuNho).AlignMiddle().Text("Hạng mục");
                                    foreach (var n in ngays)
                                        h.Cell().ColumnSpan((uint)cot.Count).Element(TieuNho).Text(n.ToString());
                                    if (cot.Count > 1)
                                        foreach (var _ in ngays)
                                            for (var k = 1; k <= cot.Count; k++)
                                                h.Cell().Element(TieuNho).Text(k.ToString());
                                });
                                foreach (var hm in mau.HangMuc.OrderBy(h => h.ThuTu))
                                {
                                    table.Cell().Element(ONho).Text(hm.Ten);
                                    foreach (var n in ngays)
                                    {
                                        var kh = maTran.GetValueOrDefault((hm.Id, n));
                                        for (var k = 0; k < cot.Count; k++)
                                        {
                                            var v = kh?[k] ?? "";
                                            var o = table.Cell().Element(ONho).AlignCenter();
                                            if (v == "O") o.Text(v).Bold().FontColor(Colors.Red.Darken2);
                                            else o.Text(v);
                                        }
                                    }
                                }
                            });

                            if (trangCuoi && ghiChu.Count > 0)
                            {
                                nd.Item().PaddingTop(8).Text("Ghi chú / Không đạt trong tháng").Bold().FontSize(9);
                                nd.Item().PaddingTop(2).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.ConstantColumn(45);
                                        cols.RelativeColumn(2);
                                        cols.RelativeColumn(5);
                                    });
                                    table.Header(h =>
                                    {
                                        h.Cell().Element(Tieu).Text("Ngày");
                                        h.Cell().Element(Tieu).Text("Hạng mục");
                                        h.Cell().Element(Tieu).Text("Nội dung");
                                    });
                                    foreach (var g in ghiChu)
                                    {
                                        table.Cell().Element(O).Text(g.Ngay.ToString("dd/MM"));
                                        table.Cell().Element(O).Text(g.HangMuc);
                                        table.Cell().Element(O).Text(g.NoiDung);
                                    }
                                });
                            }
                        });
                        page.Footer().Element(ChanTrang);
                    });
                }
            });
        }
        else
        {
            // Bảng phẳng: mỗi dòng phiếu 1 hàng; cột Ngày + [Hạng mục] + trường đầu phiếu + trường dòng.
            doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Element(TieuDe);
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(40); // Ngày
                            if (laChecklist) cols.RelativeColumn(3);
                            foreach (var _ in truongDau) cols.RelativeColumn(2);
                            foreach (var _ in truong) cols.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(Tieu).Text("Ngày");
                            if (laChecklist) h.Cell().Element(Tieu).Text("Hạng mục");
                            foreach (var t in truongDau) h.Cell().Element(Tieu).Text(TieuDeCot(t));
                            foreach (var t in truong) h.Cell().Element(Tieu).Text(TieuDeCot(t));
                        });

                        foreach (var phieu in dsPhieu)
                        {
                            var giaTriDau = DocGiaTri(phieu.GiaTriDauJson);
                            var dong = phieu.Dong.OrderBy(d => d.ThuTu).ToList();
                            if (dong.Count == 0) dong.Add(new DongGhiNhan());
                            foreach (var d in dong)
                            {
                                var giaTri = DocGiaTri(d.GiaTriJson);
                                table.Cell().Element(O).Text(phieu.Ngay.ToString("dd/MM"));
                                if (laChecklist)
                                    table.Cell().Element(O).Text(d.HangMucBieuMauId is { } hm ? hangMucTen.GetValueOrDefault(hm, "") : "");
                                foreach (var t in truongDau)
                                    table.Cell().Element(O).Text(Resolve(t, giaTriDau.GetValueOrDefault(t.Ma)));
                                foreach (var t in truong)
                                    table.Cell().Element(O).Text(Resolve(t, giaTri.GetValueOrDefault(t.Ma)));
                            }
                        }
                    });
                    page.Footer().Element(ChanTrang);
                });
            });
        }

        return doc.GeneratePdf();
    }

    /// <summary>Số cột con (ngày × trường Đạt/KĐ) tối đa trên một trang A4 ngang trước khi tách nửa tháng.</summary>
    public const int SoCotConToiDa = 62;

    /// <summary>
    /// Gom phiếu checklist trong tháng thành ma trận (hạng mục, ngày) → ký hiệu từng cột con:
    /// "X" = Đạt, "O" = Không đạt, "" = chưa kiểm. Nhiều phiếu cùng ngày: phiếu sau ghi đè ô có nhập.
    /// </summary>
    public static Dictionary<(int HangMucId, int Ngay), string[]> GomMaTranChecklist(
        IEnumerable<PhieuGhiNhan> dsPhieu, IReadOnlyList<TruongBieuMau> cot)
    {
        var kq = new Dictionary<(int, int), string[]>();
        foreach (var p in dsPhieu.OrderBy(p => p.Ngay).ThenBy(p => p.Id))
            foreach (var d in p.Dong)
            {
                if (d.HangMucBieuMauId is not { } hm) continue;
                var gt = DocGiaTri(d.GiaTriJson);
                var key = (hm, p.Ngay.Day);
                if (!kq.TryGetValue(key, out var o)) kq[key] = o = Enumerable.Repeat("", cot.Count).ToArray();
                for (var k = 0; k < cot.Count; k++)
                {
                    var kh = KyHieuDatKhongDat(gt.GetValueOrDefault(cot[k].Ma));
                    if (kh.Length > 0) o[k] = kh;
                }
            }
        return kq;
    }

    /// <summary>"Đạt" → "X", giá trị khác (Không đạt) → "O", trống → "".</summary>
    public static string KyHieuDatKhongDat(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "";
        return v.Trim().Equals("Đạt", StringComparison.OrdinalIgnoreCase) ? "X" : "O";
    }

    private sealed record GhiChuDong(DateOnly Ngay, string HangMuc, string NoiDung);

    /// <summary>Thông tin đầu phiếu + các dòng checklist có Không đạt hoặc có giá trị ở trường khác (ghi chú...).</summary>
    private static List<GhiChuDong> GomGhiChuChecklist(IEnumerable<PhieuGhiNhan> dsPhieu,
        IReadOnlyList<TruongBieuMau> truongDau, IReadOnlyList<TruongBieuMau> truong,
        IReadOnlyDictionary<int, string> hangMucTen, Func<TruongBieuMau, string?, string> resolve)
    {
        var kq = new List<GhiChuDong>();
        var dauTruoc = "";
        foreach (var p in dsPhieu.OrderBy(p => p.Ngay).ThenBy(p => p.Id))
        {
            var dau = DocGiaTri(p.GiaTriDauJson);
            var thongTinDau = string.Join("; ", truongDau
                .Select(t => (t.Ten, V: resolve(t, dau.GetValueOrDefault(t.Ma))))
                .Where(x => x.V.Length > 0).Select(x => $"{x.Ten}: {x.V}"));
            // Chỉ ghi khi đổi so với phiếu trước - tránh lặp "Khu vực: ..." mỗi ngày.
            if (thongTinDau.Length > 0 && thongTinDau != dauTruoc) kq.Add(new(p.Ngay, "(Đầu phiếu)", thongTinDau));
            dauTruoc = thongTinDau;

            foreach (var d in p.Dong.OrderBy(d => d.ThuTu))
            {
                var gt = DocGiaTri(d.GiaTriJson);
                var phan = new List<string>();
                foreach (var t in truong)
                {
                    var v = resolve(t, gt.GetValueOrDefault(t.Ma));
                    if (v.Length == 0) continue;
                    if (t.Kieu != KieuTruongBieuMau.DatKhongDat || KyHieuDatKhongDat(v) == "O")
                        phan.Add($"{t.Ten}: {v}");
                }
                if (phan.Count == 0) continue;
                var ten = d.HangMucBieuMauId is { } hm ? hangMucTen.GetValueOrDefault(hm, "(hạng mục đã xoá)") : "";
                kq.Add(new(p.Ngay, ten, string.Join("; ", phan)));
            }
        }
        return kq;
    }

    /// <summary>Thông tin in header: tên công ty, địa chỉ, logo (byte). Chưa cài đặt thì lấy hồ sơ đăng ký cơ sở.</summary>
    private sealed record ThongTinIn(string TenCongTy, string? DiaChi, byte[]? Logo);

    private async Task<ThongTinIn> LayThongTinInAsync(CancellationToken ct)
    {
        var cd = await _db.CaiDatInBieuMaus.AsNoTracking().FirstOrDefaultAsync(ct);
        var ten = cd?.TenCongTy ?? _db.TenantInfo?.Name ?? "";
        var diaChi = cd is null ? (_db.TenantInfo as Tenant)?.DiaChi : cd.DiaChi;
        return new ThongTinIn(ten, diaChi, _docAnh?.Doc(cd?.LogoDuongDan));
    }

    /// <summary>
    /// Khung header giống biểu mẫu giấy: [logo] | [TÊN CÔNG TY / Đ/c] | [Mã hiệu / Ngày ban hành / Lần ban hành].
    /// </summary>
    private static void KhungHeader(IContainer c, BieuMauEntity mau, ThongTinIn tt)
    {
        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(75);
                cols.RelativeColumn();
                cols.ConstantColumn(190);
            });

            var logo = table.Cell().RowSpan(3).Border(0.75f).Padding(3).AlignMiddle().AlignCenter();
            if (tt.Logo is not null) logo.Height(62).Image(tt.Logo).FitArea();
            else logo.Text("");

            table.Cell().RowSpan(3).Border(0.75f).PaddingHorizontal(6).AlignMiddle().Column(col =>
            {
                col.Item().AlignCenter().Text(tt.TenCongTy.ToUpper()).Bold().FontSize(12);
                if (!string.IsNullOrWhiteSpace(tt.DiaChi))
                    col.Item().PaddingTop(3).AlignCenter().Text($"Đ/c: {tt.DiaChi}").FontSize(9);
            });

            static IContainer O(IContainer x) => x.Border(0.75f).PaddingHorizontal(5).PaddingVertical(4).AlignMiddle();
            table.Cell().Element(O).Text($"Mã hiệu: {mau.MaHieu}").FontSize(9);
            table.Cell().Element(O).Text($"Ngày ban hành: {mau.NgayBanHanh?.ToString("dd/MM/yyyy")}").FontSize(9);
            table.Cell().Element(O).Text($"Lần ban hành: {mau.LanBanHanh}").FontSize(9);
        });
    }

    /// <summary>
    /// "Người lập & ký" (tên, giờ ký, hình chữ ký) bên trái; mã QR tới trang tra cứu công khai + mã xác thực bên phải.
    /// Phiếu chưa ký (nháp / lập trước khi có chức năng ký) thì ghi rõ chưa ký.
    /// </summary>
    private void KhoiKy(IContainer c, PhieuGhiNhan phieu)
    {
        if (phieu.KyLucUtc is not { } kyLuc)
        {
            c.Text(phieu.TrangThai == TrangThaiPhieu.Nhap ? "Phiếu chưa hoàn thành (nháp) - chưa ký." : "Phiếu chưa có chữ ký điện tử.")
             .Italic().FontSize(8);
            return;
        }
        var chuKy = _docAnh?.Doc(phieu.ChuKyAnh);
        var qr = string.IsNullOrEmpty(phieu.MaTraCuu) || string.IsNullOrEmpty(_gocWeb) ? null
            : new QRCoder.PngByteQRCode(new QRCoder.QRCodeGenerator().CreateQrCode(
                  $"{_gocWeb}/tra-cuu/phieu/{phieu.MaTraCuu}", QRCoder.QRCodeGenerator.ECCLevel.M)).GetGraphic(10);
        c.Row(r =>
        {
            r.RelativeItem().Column(col =>
            {
                col.Item().Text("Người lập & ký").SemiBold().FontSize(9);
                col.Item().Text(phieu.TenNguoiKy ?? "").Bold().FontSize(10);
                col.Item().Text($"Ký điện tử lúc {GioVietNam.TuUtc(kyLuc):HH:mm dd/MM/yyyy}").FontSize(8);
                if (chuKy is not null) col.Item().PaddingTop(2).Height(45).AlignLeft().Image(chuKy).FitArea();
            });
            if (qr is not null)
                r.ConstantItem(230).Row(q =>
                {
                    q.ConstantItem(70).Height(70).Image(qr).FitArea();
                    q.RelativeItem().PaddingLeft(6).AlignMiddle().Column(t =>
                    {
                        t.Item().Text("Quét mã để đối chiếu bản gốc").FontSize(8).SemiBold();
                        t.Item().Text($"Mã xác thực: {phieu.MaBamNoiDung?[..12]}").FontSize(7);
                    });
                });
        });
    }

    private static string TieuDeCot(TruongBieuMau t)
    {
        var s = string.IsNullOrWhiteSpace(t.Nhom) ? t.Ten : $"{t.Nhom} - {t.Ten}";
        return string.IsNullOrWhiteSpace(t.DonVi) ? s : $"{s} ({t.DonVi})";
    }

    private static Dictionary<string, string?> DocGiaTri(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static IContainer Tieu(IContainer c) =>
        c.Border(0.5f).Background(Colors.Grey.Lighten2).Padding(4).AlignCenter();

    private static IContainer O(IContainer c) =>
        c.Border(0.5f).Padding(4);

    private static IContainer TieuNho(IContainer c) =>
        c.Border(0.5f).Background(Colors.Grey.Lighten2).Padding(1).AlignCenter();

    private static IContainer ONho(IContainer c) =>
        c.Border(0.5f).PaddingVertical(1).PaddingHorizontal(2);
}
