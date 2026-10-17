import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Gọi nhóm API /api/v1/suat-an - CHỈ ĐỌC. Khai báo Món ăn, Thực đơn vẫn làm trên web.
class SuatAnData {
  final ApiClient _api;

  SuatAnData(this._api);

  /// Thực đơn + số suất của một ngày; bỏ trống [ngay] thì máy chủ lấy hôm nay theo giờ Việt Nam.
  Future<SuatAnNgay> theoNgay(DateTime? ngay) async => SuatAnNgay.tuJson(
        await _api.get('/api/v1/suat-an',
            thamSo: ngay == null
                ? null
                : {'ngay': '${ngay.year.toString().padLeft(4, '0')}-'
                    '${ngay.month.toString().padLeft(2, '0')}-'
                    '${ngay.day.toString().padLeft(2, '0')}'}) as Map<String, dynamic>,
      );
}

final suatAnProvider = Provider<SuatAnData>((ref) => SuatAnData(ref.watch(apiProvider)));

/// Bản tin của một ngày; null = hôm nay.
final suatAnNgayProvider = FutureProvider.autoDispose
    .family<SuatAnNgay, DateTime?>((ref, ngay) => ref.watch(suatAnProvider).theoNgay(ngay));
