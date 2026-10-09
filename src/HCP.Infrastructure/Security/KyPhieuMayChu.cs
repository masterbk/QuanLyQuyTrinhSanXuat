using System.Security.Cryptography;
using System.Text;

namespace HCP.Infrastructure.Security;

/// <summary>
/// Chữ ký số của máy chủ trên phiếu biểu mẫu đã ký (ECDSA P-256 / SHA-256). Ký lên cặp (mã tra cứu, mã băm nội
/// dung) lúc nhân viên Hoàn thành phiếu; trang quét QR xác minh bằng khoá công khai.
///
/// Khoá bí mật nằm trong FILE ngoài CSDL (xem <see cref="TaiHoacTao"/>), nên người chỉ có quyền CSDL (sửa bằng SQL,
/// khôi phục bản sao lưu đã chỉnh) sửa dữ liệu rồi tính lại mã băm cũng không tạo được chữ ký hợp lệ. Người có quyền
/// quản trị máy chủ đọc được file khoá thì vẫn giả được - muốn chống cả họ phải dùng chữ ký số PKI.
/// </summary>
public interface IKyPhieuMayChu
{
    /// <summary>Chữ ký (base64url) cho phiếu có mã tra cứu + mã băm này.</summary>
    string Ky(string maTraCuu, string maBam);

    /// <summary>Chữ ký có đúng do khoá của máy chủ ký lên đúng (mã tra cứu, mã băm) này không.</summary>
    bool XacMinh(string maTraCuu, string maBam, string? chuKy);

    /// <summary>
    /// True khi chưa ký bù các phiếu đã ký trước khi có khoá này. Dấu "đã ký bù" là file cạnh file khoá (ngoài CSDL),
    /// nên người chỉ có quyền CSDL không kích hoạt lại được việc ký bù để hợp thức hoá dữ liệu đã sửa.
    /// </summary>
    bool CanKyBu { get; }

    /// <summary>Ghi dấu đã ký bù xong - gọi SAU khi ký bù thành công.</summary>
    void DanhDauDaKyBu();
}

public sealed class KyPhieuMayChu : IKyPhieuMayChu
{
    private readonly ECDsa _khoa;
    private readonly string? _fileDauKyBu;

    /// <param name="fileDauKyBu">File dấu "đã ký bù" (null = không cần ký bù, vd khoá tạo trong bộ nhớ khi kiểm thử).</param>
    public KyPhieuMayChu(ECDsa khoa, string? fileDauKyBu = null)
    {
        _khoa = khoa;
        _fileDauKyBu = fileDauKyBu;
    }

    public bool CanKyBu => _fileDauKyBu is not null && !File.Exists(_fileDauKyBu);

    public void DanhDauDaKyBu()
    {
        if (_fileDauKyBu is not null) File.WriteAllText(_fileDauKyBu, DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Đọc khoá bí mật (PEM) ở <paramref name="duongDan"/>; chưa có thì sinh khoá mới và ghi ra file đó.
    /// File phải nằm NGOÀI thư mục deploy và ngoài CSDL. MẤT FILE = mọi phiếu đã ký báo chữ ký không hợp lệ, nên
    /// cần sao lưu file này (cất ở nơi an toàn, không để cùng bản sao lưu CSDL).
    /// </summary>
    public static KyPhieuMayChu TaiHoacTao(string duongDan)
    {
        var khoa = ECDsa.Create();
        if (File.Exists(duongDan))
            khoa.ImportFromPem(File.ReadAllText(duongDan));
        else
        {
            khoa.GenerateKey(ECCurve.NamedCurves.nistP256);
            Directory.CreateDirectory(Path.GetDirectoryName(duongDan)!);
            File.WriteAllText(duongDan, khoa.ExportECPrivateKeyPem());
        }
        return new KyPhieuMayChu(khoa, duongDan + ".da-ky-bu");
    }

    public string Ky(string maTraCuu, string maBam) =>
        Base64Url(_khoa.SignData(NoiDung(maTraCuu, maBam), HashAlgorithmName.SHA256));

    public bool XacMinh(string maTraCuu, string maBam, string? chuKy)
    {
        if (string.IsNullOrWhiteSpace(chuKy)) return false;
        byte[] sig;
        try { sig = TuBase64Url(chuKy); }
        catch (FormatException) { return false; }
        return _khoa.VerifyData(NoiDung(maTraCuu, maBam), sig, HashAlgorithmName.SHA256);
    }

    // Gắn mã tra cứu vào nội dung ký để không chép được chữ ký của phiếu này sang phiếu khác.
    private static byte[] NoiDung(string maTraCuu, string maBam) =>
        Encoding.UTF8.GetBytes($"HCP-PHIEU-v1|{maTraCuu}|{maBam}");

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] TuBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
