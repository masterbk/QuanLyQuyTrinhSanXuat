namespace HCP.Domain.Constants;

public static class AppClaimTypes
{
    /// <summary>
    /// Id thật của cơ sở (khoá của bảng Tenants). Đây là giá trị được ghi vào cột TenantId
    /// của mọi bảng nghiệp vụ, nên code ứng dụng dùng claim này khi cần Id.
    /// </summary>
    public const string TenantId = "tenantId";

    /// <summary>
    /// Identifier (slug) của cơ sở. Finbuckle ClaimStrategy đọc CHÍNH claim này, vì kho lưu
    /// tenant tra cứu bằng TryGetByIdentifierAsync - tức là so khớp cột Identifier, không phải Id.
    ///
    /// Phải tách làm hai claim: đưa nhầm Id vào đây thì Finbuckle không tìm ra cơ sở,
    /// TenantInfo là null và mọi truy vấn có bộ lọc tenant sẽ ném NullReferenceException.
    /// </summary>
    public const string TenantIdentifier = "tenantIdentifier";
}
