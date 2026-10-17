import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import '../lenh_san_xuat/mo_hinh.dart' show soGon;
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

/// Màn Suất ăn: hôm nay nấu món gì và phải nấu bao nhiêu suất cho từng trường.
/// Chỉ xem - khai báo Món ăn, Thực đơn làm trên web (nhập liệu nặng, đẩy thẳng sang HanoiCheck).
class ManSuatAn extends ConsumerStatefulWidget {
  const ManSuatAn({super.key});

  @override
  ConsumerState<ManSuatAn> createState() => _ManSuatAnState();
}

class _ManSuatAnState extends ConsumerState<ManSuatAn> {
  /// null = hôm nay (để máy chủ tự lấy theo giờ Việt Nam, khỏi lệch khi điện thoại sai múi giờ).
  DateTime? _ngay;

  Future<void> _chonNgay() async {
    final nay = bayGioVietNam();
    final chon = await showDatePicker(
      context: context,
      initialDate: _ngay ?? nay,
      firstDate: DateTime(nay.year - 1),
      lastDate: DateTime(nay.year + 1),
      helpText: 'Xem suất ăn ngày',
    );
    if (chon != null) setState(() => _ngay = chon);
  }

  @override
  Widget build(BuildContext context) {
    final banTin = ref.watch(suatAnNgayProvider(_ngay));

    return Scaffold(
      appBar: AppBar(
        title: const Text('Suất ăn'),
        actions: [
          if (_ngay != null)
            IconButton(
              tooltip: 'Về hôm nay',
              icon: const Icon(Icons.today),
              onPressed: () => setState(() => _ngay = null),
            ),
          IconButton(tooltip: 'Chọn ngày', icon: const Icon(Icons.calendar_month), onPressed: _chonNgay),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(suatAnNgayProvider(_ngay)),
        child: banTin.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => _Loi(
            thongBao: e is LoiApi ? e.thongBao : 'Không tải được suất ăn: $e',
            thuLai: () => ref.invalidate(suatAnNgayProvider(_ngay)),
          ),
          data: (d) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
            children: [
              _ThanhNgay(ngay: d.ngay, tenThu: d.tenThu),
              const SizedBox(height: 12),
              if (d.trong)
                const Card(
                  child: Padding(
                    padding: EdgeInsets.all(20),
                    child: Text(
                      'Ngày này chưa có thực đơn và chưa có đơn suất ăn nào.',
                      textAlign: TextAlign.center,
                    ),
                  ),
                )
              else ...[
                if (d.theoKhachHang.isNotEmpty) ...[
                  _TieuDe('Số suất phải nấu', phu: '${soGon(d.tongSuat, 0)} suất'),
                  for (final k in d.theoKhachHang) _TheKhach(khach: k),
                  if (d.tongTheoMon.length > 1) _TongTheoMon(mon: d.tongTheoMon),
                  const SizedBox(height: 16),
                ],
                if (d.thucDon.isNotEmpty) ...[
                  _TieuDe('Thực đơn'),
                  for (final td in d.thucDon) _TheThucDon(thucDon: td),
                ] else
                  const Card(
                    child: ListTile(
                      leading: Icon(Icons.restaurant_menu_outlined),
                      title: Text('Chưa khai thực đơn cho thứ này'),
                      subtitle: Text('Khai trên web ở màn Thực đơn.'),
                    ),
                  ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ThanhNgay extends StatelessWidget {
  final DateTime ngay;
  final String tenThu;

  const _ThanhNgay({required this.ngay, required this.tenThu});

  @override
  Widget build(BuildContext context) => Card(
        margin: EdgeInsets.zero,
        color: Theme.of(context).colorScheme.primaryContainer,
        child: ListTile(
          leading: const Icon(Icons.event),
          title: Text('$tenThu, ${_ngayVn.format(ngay)}',
              style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: const Text('Bấm biểu tượng lịch ở trên để xem ngày khác'),
        ),
      );
}

class _TieuDe extends StatelessWidget {
  final String chu;
  final String? phu;

  const _TieuDe(this.chu, {this.phu});

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(4, 8, 4, 6),
        child: Row(
          children: [
            Text(chu, style: Theme.of(context).textTheme.titleMedium),
            const Spacer(),
            if (phu != null)
              Text(phu!,
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(color: Theme.of(context).colorScheme.primary)),
          ],
        ),
      );
}

/// Một trường: tổng suất ở dòng đầu, mở ra xem từng món.
class _TheKhach extends StatelessWidget {
  final SuatTheoKhach khach;

  const _TheKhach({required this.khach});

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 6),
        child: ExpansionTile(
          leading: const Icon(Icons.school_outlined),
          title: Text(khach.tenKhachHang, style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text('${soGon(khach.tongSuat, 0)} suất · ${khach.danhSachMon.length} món'),
          childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
          children: [
            for (final m in khach.danhSachMon)
              ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                title: Text(m.tenMonAn),
                trailing: Text('${soGon(m.soSuat, 0)} suất'),
              ),
          ],
        ),
      );
}

class _TongTheoMon extends StatelessWidget {
  final List<SuatTheoMon> mon;

  const _TongTheoMon({required this.mon});

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 6),
        child: ExpansionTile(
          leading: const Icon(Icons.summarize_outlined),
          title: const Text('Tổng theo món (gộp mọi trường)'),
          childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
          children: [
            for (final m in mon)
              ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                title: Text(m.tenMonAn),
                trailing: Text('${soGon(m.soSuat, 0)} suất',
                    style: const TextStyle(fontWeight: FontWeight.bold)),
              ),
          ],
        ),
      );
}

class _TheThucDon extends StatelessWidget {
  final ThucDonTrongNgay thucDon;

  const _TheThucDon({required this.thucDon});

  @override
  Widget build(BuildContext context) {
    final theoBua = thucDon.monTheoBua;
    return Card(
      margin: const EdgeInsets.only(bottom: 6),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(thucDon.tenThucDon, style: const TextStyle(fontWeight: FontWeight.bold)),
            Text(
              thucDon.tenTruong.isEmpty
                  ? '${thucDon.tenNhomTuoi} · dùng chung mọi trường'
                  : '${thucDon.tenNhomTuoi} · ${thucDon.tenTruong.join(', ')}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 8),
            for (final bua in theoBua.entries) ...[
              Text(bua.key, style: Theme.of(context).textTheme.labelLarge),
              Padding(
                padding: const EdgeInsets.only(left: 8, top: 2, bottom: 6),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [for (final m in bua.value) Text('• ${m.tenMonAn}')],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Loi extends StatelessWidget {
  final String thongBao;
  final VoidCallback thuLai;

  const _Loi({required this.thongBao, required this.thuLai});

  @override
  Widget build(BuildContext context) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
        children: [
          Text(thongBao, textAlign: TextAlign.center),
          const SizedBox(height: 12),
          Center(child: FilledButton.tonal(onPressed: thuLai, child: const Text('Thử lại'))),
        ],
      );
}
