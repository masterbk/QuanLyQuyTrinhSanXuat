namespace HCP.Domain.Enums;

/// <summary>Kiểu bố cục của một biểu mẫu kiểm soát (quyết định cách nhập liệu).</summary>
public enum BoCucBieuMau
{
    /// <summary>Theo ngày: mỗi phiếu ghi cho một ngày/lần, các trường điền trực tiếp (vd nhiệt độ tủ, đèn UV, vệ sinh xe).</summary>
    TheoNgay = 0,

    /// <summary>Checklist: danh sách hạng mục cố định, mỗi hạng mục tick/điền theo các trường (vd check list vệ sinh).</summary>
    Checklist = 1,

    /// <summary>Nhiều dòng tự do: mỗi phiếu gồm nhiều dòng do người nhập thêm (vd tiếp nhận nguyên liệu, công đoạn nướng).</summary>
    NhieuDongTuDo = 2
}
