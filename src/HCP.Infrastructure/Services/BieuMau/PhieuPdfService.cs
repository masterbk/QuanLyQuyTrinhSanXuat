using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
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
}

public sealed class PhieuPdfService : IPhieuPdfService
{
    private readonly IPhieuGhiNhanService _phieu;
    private readonly AppDbContext _db;
    private readonly IDanhMucService<Staff> _nhanSu;
    private readonly IDanhMucService<Product> _sanPham;
    private readonly IDanhMucService<SubSupplier> _ncc;
    private readonly IDanhMucService<Facility> _coSo;

    public PhieuPdfService(IPhieuGhiNhanService phieu, AppDbContext db,
                           IDanhMucService<Staff> nhanSu, IDanhMucService<Product> sanPham,
                           IDanhMucService<SubSupplier> ncc, IDanhMucService<Facility> coSo)
    {
        _phieu = phieu;
        _db = db;
        _nhanSu = nhanSu;
        _sanPham = sanPham;
        _ncc = ncc;
        _coSo = coSo;
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
        var tenCongTy = _db.TenantInfo?.Name ?? "";

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
                _ => raw
            };
        }

        var truong = mau.Truong.OrderBy(t => t.ThuTu).ToList();
        var hangMucTen = mau.HangMuc.ToDictionary(h => h.Id, h => h.Ten);
        var laChecklist = mau.BoCuc == BoCucBieuMau.Checklist;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(tenCongTy))
                        col.Item().Text(tenCongTy).Bold().FontSize(11);
                    col.Item().Row(r =>
                    {
                        r.RelativeItem();
                        r.ConstantItem(220).AlignRight().Text($"Mã hiệu: {mau.MaHieu}").FontSize(9);
                    });
                    col.Item().PaddingTop(2).AlignCenter().Text(mau.Ten).Bold().FontSize(14);
                    col.Item().PaddingTop(2).Text(t =>
                    {
                        t.Span("Ngày: ").SemiBold();
                        t.Span($"{phieu.Ngay:dd/MM/yyyy}   ");
                        if (!string.IsNullOrWhiteSpace(phieu.Ca)) { t.Span("Ca: ").SemiBold(); t.Span($"{phieu.Ca}   "); }
                        if (!string.IsNullOrWhiteSpace(phieu.KhuVuc)) { t.Span("Khu vực: ").SemiBold(); t.Span($"{phieu.KhuVuc}   "); }
                        if (!string.IsNullOrWhiteSpace(phieu.NguoiLap))
                        {
                            t.Span("Người lập: ").SemiBold();
                            t.Span(nhanSu.GetValueOrDefault(phieu.NguoiLap!, phieu.NguoiLap!));
                        }
                    });
                    col.Item().PaddingTop(4);
                });

                page.Content().Table(table =>
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
}
