using System.Security.Cryptography;
using System.Text;
using HCP.Infrastructure.HanoiCheck;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng ký HMAC đúng đặc tả HanoiCheck (mục 1.2).
///
/// Chữ ký sai là lỗi âm thầm: build vẫn xanh, chỉ khi HnC trả 401 INVALID_SIGNATURE mới lộ.
/// Vì vậy đối chiếu với "golden vector" tính ĐỘC LẬP bằng Python (openssl/hashlib) bên ngoài,
/// không phải tự tính lại bằng chính code đang test.
/// </summary>
public class HmacSignerTests
{
    // Vector chuẩn tính bằng Python:
    //   secret='hmac_secret_demo', method='POST',
    //   path='/api/supplier/warehouses/merge', ts='1706600000',
    //   nonce='8f14e45fceea167a5a36c1d2', body='[{"ma_kho":"KHO01","ten_kho":"Kho A"}]'
    private const string Secret = "hmac_secret_demo";
    private const string Method = "POST";
    private const string Path = "/api/supplier/warehouses/merge";
    private const string Timestamp = "1706600000";
    private const string Nonce = "8f14e45fceea167a5a36c1d2";
    private const string Body = "[{\"ma_kho\":\"KHO01\",\"ten_kho\":\"Kho A\"}]";
    private const string ChuKyChuan = "yDrVrWucC7MO6bD62wqD9rkm6sADL1FRrmRvORlFL+A=";

    private readonly HmacSigner _signer = new();

    [Fact]
    public void TinhChuKy_Khop_Golden_Vector()
    {
        var chuKy = _signer.TinhChuKy(Secret, Method, Path, Timestamp, Nonce, Body);
        Assert.Equal(ChuKyChuan, chuKy);
    }

    [Fact]
    public void Body_Rong_Van_Bam_SHA256_Cua_Chuoi_Rong()
    {
        // Với GET / body rỗng, canonical string dùng SHA256_HEX của chuỗi rỗng.
        // Tính tay chữ ký kỳ vọng để chắc chắn không có nhánh xử lý đặc biệt sai.
        const string emptyHex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var canonical = string.Join('\n', Method, Path, Timestamp, Nonce, emptyHex);
        var mongDoi = Convert.ToBase64String(
            new HMACSHA256(Encoding.UTF8.GetBytes(Secret))
                .ComputeHash(Encoding.UTF8.GetBytes(canonical)));

        var chuKy = _signer.TinhChuKy(Secret, Method, Path, Timestamp, Nonce, string.Empty);
        Assert.Equal(mongDoi, chuKy);
    }

    [Fact]
    public void ChuKy_La_Base64_Hop_Le()
    {
        var chuKy = _signer.TinhChuKy(Secret, Method, Path, Timestamp, Nonce, Body);
        // Base64 của HMAC-SHA256 (32 byte) luôn dài 44 ký tự, giải mã lại được đúng 32 byte.
        var bytes = Convert.FromBase64String(chuKy);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void Secret_Khac_Nhau_Cho_Chu_Ky_Khac_Nhau()
    {
        // Đây là hàng rào chống dùng nhầm secret giữa các cơ sở: cùng nội dung request
        // nhưng secret của cơ sở khác PHẢI ra chữ ký khác, để HnC từ chối request giả.
        var chuKyA = _signer.TinhChuKy("secret-coso-A", Method, Path, Timestamp, Nonce, Body);
        var chuKyB = _signer.TinhChuKy("secret-coso-B", Method, Path, Timestamp, Nonce, Body);
        Assert.NotEqual(chuKyA, chuKyB);
    }

    [Fact]
    public void Doi_Bat_Ky_Thanh_Phan_Nao_Cung_Doi_Chu_Ky()
    {
        var goc = _signer.TinhChuKy(Secret, Method, Path, Timestamp, Nonce, Body);
        Assert.NotEqual(goc, _signer.TinhChuKy(Secret, "GET", Path, Timestamp, Nonce, Body));
        Assert.NotEqual(goc, _signer.TinhChuKy(Secret, Method, "/api/supplier/steps/merge", Timestamp, Nonce, Body));
        Assert.NotEqual(goc, _signer.TinhChuKy(Secret, Method, Path, "1706600001", Nonce, Body));
        Assert.NotEqual(goc, _signer.TinhChuKy(Secret, Method, Path, Timestamp, "nonce-khac", Body));
        Assert.NotEqual(goc, _signer.TinhChuKy(Secret, Method, Path, Timestamp, Nonce, Body + " "));
    }

    [Fact]
    public void Ky_Sinh_Nonce_Ngau_Nhien_Va_Timestamp_Hien_Tai()
    {
        var h1 = _signer.Ky(Secret, Method, Path, Body);
        var h2 = _signer.Ky(Secret, Method, Path, Body);

        // Nonce phải khác nhau mỗi lần (chống replay).
        Assert.NotEqual(h1.Nonce, h2.Nonce);

        // Timestamp là Unix giây hợp lệ, gần thời điểm hiện tại.
        var ts = long.Parse(h1.Timestamp);
        var lech = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts);
        Assert.True(lech < 5, $"Timestamp lệch {lech}s so với hiện tại.");

        // Chữ ký sinh ra phải khớp với nhân tất định trên cùng timestamp + nonce.
        Assert.Equal(_signer.TinhChuKy(Secret, Method, Path, h1.Timestamp, h1.Nonce, Body), h1.Signature);
    }

    [Fact]
    public void Thieu_Secret_Thi_Nem_Loi()
    {
        Assert.Throws<ArgumentException>(
            () => _signer.TinhChuKy("", Method, Path, Timestamp, Nonce, Body));
    }
}
