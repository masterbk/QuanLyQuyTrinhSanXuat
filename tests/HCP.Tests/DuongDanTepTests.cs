using System.Text.Json;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Sync;

namespace HCP.Tests;

/// <summary>
/// Quy định đường dẫn tệp của HanoiCheck (đặc tả v2.2, Phần III mục 5) và bước đổi đường dẫn tương đối
/// "/uploads/..." thành URL tuyệt đối trước khi gửi.
/// </summary>
public class DuongDanTepTests
{
    [Theory]
    [InlineData("https://a.vn/x.jpg", true)]
    [InlineData("https://a.vn/x.JPEG?w=100", true)]
    [InlineData("https://a.vn/x.webp#m", true)]
    [InlineData("https://a.vn/x.pdf", false)]
    [InlineData("https://drive.google.com/file/d/1/x.png", false)]
    [InlineData("", false)]
    public void Anh_Lo_Chi_Nhan_Duoi_Anh_Khong_Nhan_Google_Drive(string duongDan, bool hopLe) =>
        Assert.Equal(hopLe, HnCPayloadMapper.LaDuongDanAnhLo(duongDan));

    [Theory]
    [InlineData("https://a.vn/x.png", true)]
    [InlineData("https://a.vn/giay.PDF", true)]
    [InlineData("https://a.vn/hd.docx", true)]
    [InlineData("https://drive.google.com/file/d/abc/view", true)]
    [InlineData("https://a.vn/bang.xlsx", false)]
    public void Tep_Khau_Nhan_Anh_Pdf_Word_Hoac_Google_Drive(string duongDan, bool hopLe) =>
        Assert.Equal(hopLe, HnCPayloadMapper.LaDuongDanTepKhau(duongDan));

    private const string Payload =
        "[{\"ma_lo\":\"LO-05\",\"ten_lo\":\"Lô Hòa Bình\",\"danh_sach_anh\":[{\"ten_anh\":\"a\",\"duong_dan\":\"/uploads/t1/a.png\",\"loai\":\"image/png\"}]," +
        "\"danh_sach_khau\":[{\"ma_buoc_sx\":\"B1\",\"danh_sach_files\":[{\"ma_file\":\"F1\",\"duong_dan\":\"https://x.vn/kd.pdf\"}]}]}]";

    [Fact]
    public void Doi_Duong_Dan_Tuong_Doi_Thanh_Url_Tuyet_Doi_Giu_Nguyen_Url_San_Co()
    {
        var (json, conTuongDoi) = SyncOutboxProcessor.ChuanHoaDuongDan(Payload, "https://app.congty.vn/");

        Assert.Null(conTuongDoi);
        var lo = JsonDocument.Parse(json).RootElement[0];
        Assert.Equal("https://app.congty.vn/uploads/t1/a.png",
                     lo.GetProperty("danh_sach_anh")[0].GetProperty("duong_dan").GetString());
        Assert.Equal("https://x.vn/kd.pdf",
                     lo.GetProperty("danh_sach_khau")[0].GetProperty("danh_sach_files")[0].GetProperty("duong_dan").GetString());
        Assert.Contains("Lô Hòa Bình", json);   // không bị mã hoá \u khi ghi lại
    }

    [Fact]
    public void Chua_Cau_Hinh_Base_Url_Thi_Bao_Duong_Dan_Tuong_Doi_Va_Giu_Nguyen_Payload()
    {
        var (json, conTuongDoi) = SyncOutboxProcessor.ChuanHoaDuongDan(Payload, null);

        Assert.Equal("/uploads/t1/a.png", conTuongDoi);
        Assert.Equal(Payload, json);
    }

    [Fact]
    public void Payload_Khong_Co_Duong_Dan_Tuong_Doi_Thi_Khong_Doi_Gi()
    {
        const string coSo = "[{\"ma_co_so\":\"CS-0001\",\"ten_co_so\":\"Xưởng\"}]";
        Assert.Equal((coSo, (string?)null), SyncOutboxProcessor.ChuanHoaDuongDan(coSo, null));

        const string daTuyetDoi = "[{\"images\":[{\"path_file\":\"https://a.vn/x.jpg\"}]}]";
        Assert.Equal((daTuyetDoi, (string?)null), SyncOutboxProcessor.ChuanHoaDuongDan(daTuyetDoi, null));
    }
}
