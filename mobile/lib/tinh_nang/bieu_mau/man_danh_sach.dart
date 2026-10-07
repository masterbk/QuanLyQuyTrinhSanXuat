import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_nhac_han.dart';
import 'man_nhap_phieu.dart';
import 'mo_hinh.dart';

/// Danh sách biểu mẫu kiểm soát nhân viên được phép điền; chạm vào một mẫu để nhập phiếu.
/// Hiện trạng thái "hôm nay" theo từng mẫu và cho lọc nhanh các mẫu chưa nhập hôm nay.
class ManDanhSachBieuMau extends ConsumerStatefulWidget {
  const ManDanhSachBieuMau({super.key});

  @override
  ConsumerState<ManDanhSachBieuMau> createState() => _ManDanhSachBieuMauState();
}

class _ManDanhSachBieuMauState extends ConsumerState<ManDanhSachBieuMau> {
  bool _chiChuaNhap = false; // lọc: chỉ hiện mẫu chưa nhập xong hôm nay

  /// Mẫu "chưa nhập xong hôm nay": 1 phiếu/ngày thì chưa có phiếu đã hoàn thành; nhiều phiếu/ngày thì chưa có phiếu nào.
  bool _chuaXong(BieuMau mau, List<PhieuGhiNhan> homNay) {
    final cua = homNay.where((p) => p.bieuMauId == mau.id);
    return mau.motPhieuMoiNgay ? !cua.any((p) => p.laHoanThanh) : cua.isEmpty;
  }

  @override
  Widget build(BuildContext context) {
    final ds = ref.watch(bieuMauProvider);
    final homNay = ref.watch(phieuHomNayProvider).value ?? const <PhieuGhiNhan>[];
    final nhac = ref.watch(nhacHanProvider(30)).value ?? const <NhacHan>[];

    return Scaffold(
      body: ds.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _Loi(
          thongBao: e is LoiApi ? e.thongBao : 'Không tải được biểu mẫu: $e',
          thuLai: () => ref.invalidate(bieuMauProvider),
        ),
        data: (list) {
          if (list.isEmpty) return _Rong();
          final hienThi = _chiChuaNhap ? list.where((m) => _chuaXong(m, homNay)).toList() : list;
          final soChuaXong = list.where((m) => _chuaXong(m, homNay)).length;
          return RefreshIndicator(
            onRefresh: () async {
              ref.invalidate(bieuMauProvider);
              ref.invalidate(phieuHomNayProvider);
            },
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
              children: [
                if (nhac.isNotEmpty) ...[
                  _BangNhacHan(soMuc: nhac.length, soQuaHan: nhac.where((n) => n.quaHan).length),
                  const SizedBox(height: 8),
                ],
                _ThanhLoc(
                  chiChuaNhap: _chiChuaNhap,
                  soChuaXong: soChuaXong,
                  onDoi: (v) => setState(() => _chiChuaNhap = v),
                ),
                const SizedBox(height: 8),
                if (hienThi.isEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 40),
                    child: Text('Hôm nay đã nhập hết các biểu mẫu.',
                        textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium),
                  )
                else
                  ...hienThi.map((m) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: _The(mau: m, trangThai: _trangThaiHomNay(m, homNay)),
                      )),
              ],
            ),
          );
        },
      ),
    );
  }

  _TrangThaiHomNay _trangThaiHomNay(BieuMau mau, List<PhieuGhiNhan> homNay) {
    final cua = homNay.where((p) => p.bieuMauId == mau.id).toList();
    if (!mau.motPhieuMoiNgay) {
      return cua.isEmpty
          ? const _TrangThaiHomNay('Chưa nhập', _MauCo.xam)
          : _TrangThaiHomNay('Hôm nay: ${cua.length}', _MauCo.xanh);
    }
    if (cua.isEmpty) return const _TrangThaiHomNay('Chưa nhập', _MauCo.xam);
    return cua.first.laHoanThanh
        ? const _TrangThaiHomNay('Đã nhập hôm nay', _MauCo.xanh)
        : const _TrangThaiHomNay('Đang nhập dở', _MauCo.cam);
  }
}

enum _MauCo { xam, cam, xanh }

class _TrangThaiHomNay {
  final String nhan;
  final _MauCo mau;
  const _TrangThaiHomNay(this.nhan, this.mau);
}

class _ThanhLoc extends StatelessWidget {
  final bool chiChuaNhap;
  final int soChuaXong;
  final ValueChanged<bool> onDoi;
  const _ThanhLoc({required this.chiChuaNhap, required this.soChuaXong, required this.onDoi});

  @override
  Widget build(BuildContext context) => Card(
        margin: EdgeInsets.zero,
        child: SwitchListTile(
          dense: true,
          value: chiChuaNhap,
          onChanged: onDoi,
          title: const Text('Chỉ hiện chưa nhập hôm nay'),
          subtitle: Text('Còn $soChuaXong biểu mẫu chưa nhập hôm nay'),
          secondary: const Icon(Icons.today),
        ),
      );
}

class _BangNhacHan extends StatelessWidget {
  final int soMuc;
  final int soQuaHan;
  const _BangNhacHan({required this.soMuc, required this.soQuaHan});

  @override
  Widget build(BuildContext context) => Card(
        margin: EdgeInsets.zero,
        color: Colors.red.shade50,
        child: ListTile(
          leading: Icon(Icons.notifications_active, color: Colors.red.shade700),
          title: Text('$soMuc thiết bị đến/quá hạn',
              style: TextStyle(fontWeight: FontWeight.bold, color: Colors.red.shade900)),
          subtitle: Text(soQuaHan > 0 ? 'Trong đó $soQuaHan đã quá hạn' : 'Trong 30 ngày tới'),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ManNhacHan())),
        ),
      );
}

class _The extends ConsumerWidget {
  final BieuMau mau;
  final _TrangThaiHomNay trangThai;
  const _The({required this.mau, required this.trangThai});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          final tb = await Navigator.push<String>(
              context, MaterialPageRoute(builder: (_) => ManNhapPhieu(mau: mau)));
          if (!context.mounted) return;
          ref.invalidate(phieuHomNayProvider); // cập nhật lại trạng thái hôm nay sau khi nhập
          if (tb != null) {
            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
          }
        },
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              CircleAvatar(
                backgroundColor: Theme.of(context).colorScheme.secondaryContainer,
                child: Icon(_bieuTuong(mau.boCuc), color: Theme.of(context).colorScheme.onSecondaryContainer),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(mau.ten, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                    const SizedBox(height: 2),
                    Text(mau.maHieu, style: Theme.of(context).textTheme.bodySmall),
                    if ((mau.tanSuat ?? '').isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text('Tần suất: ${mau.tanSuat}', style: Theme.of(context).textTheme.bodySmall),
                      ),
                    const SizedBox(height: 6),
                    _ChipTrangThai(trangThai: trangThai),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }

  static IconData _bieuTuong(String boCuc) => switch (boCuc) {
        'Checklist' => Icons.checklist,
        'NhieuDongTuDo' => Icons.table_rows,
        _ => Icons.event_note,
      };
}

class _ChipTrangThai extends StatelessWidget {
  final _TrangThaiHomNay trangThai;
  const _ChipTrangThai({required this.trangThai});

  @override
  Widget build(BuildContext context) {
    final (bg, fg, icon) = switch (trangThai.mau) {
      _MauCo.xanh => (Colors.green.shade100, Colors.green.shade900, Icons.check_circle),
      _MauCo.cam => (Colors.orange.shade100, Colors.orange.shade900, Icons.edit_note),
      _MauCo.xam => (Theme.of(context).colorScheme.surfaceContainerHighest,
          Theme.of(context).colorScheme.onSurfaceVariant, Icons.radio_button_unchecked),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(20)),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(icon, size: 14, color: fg),
        const SizedBox(width: 4),
        Text(trangThai.nhan, style: TextStyle(fontSize: 12, color: fg, fontWeight: FontWeight.w600)),
      ]),
    );
  }
}

class _Rong extends StatelessWidget {
  @override
  Widget build(BuildContext context) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
        children: [
          Icon(Icons.assignment_outlined, size: 56, color: Theme.of(context).hintColor),
          const SizedBox(height: 12),
          Text('Chưa có biểu mẫu nào cho bạn',
              textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 4),
          Text('Quản trị cơ sở khai báo biểu mẫu trên web (mục Biểu mẫu kiểm soát) và gán quyền cho bạn.',
              textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodySmall),
        ],
      );
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
          Center(child: FilledButton(onPressed: thuLai, child: const Text('Thử lại'))),
        ],
      );
}
