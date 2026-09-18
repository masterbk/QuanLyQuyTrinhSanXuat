/// Giờ Việt Nam (UTC+7, không có giờ mùa hè) - dùng thay cho toLocal()/DateTime.now() để hiển thị đúng
/// giờ Việt Nam kể cả khi điện thoại đặt múi giờ khác.
library;

const _lech = Duration(hours: 7);

/// Đổi mốc thời gian máy chủ trả về (UTC) sang giờ Việt Nam. Chuỗi không có "Z" (máy chủ bản cũ) cũng coi là UTC.
DateTime gioVietNam(DateTime utc) {
  final u = utc.isUtc
      ? utc
      : DateTime.utc(utc.year, utc.month, utc.day, utc.hour, utc.minute, utc.second, utc.millisecond);
  final vn = u.add(_lech);
  // Trả về DateTime thường mang đúng giờ Việt Nam để định dạng/so sánh ngày như giờ địa phương.
  return DateTime(vn.year, vn.month, vn.day, vn.hour, vn.minute, vn.second, vn.millisecond);
}

/// Thời điểm hiện tại theo giờ Việt Nam.
DateTime bayGioVietNam() => gioVietNam(DateTime.now().toUtc());
