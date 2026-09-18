import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Một khâu đang sửa trên màn hình: ai làm, làm ở cơ sở nào. Dùng chung cho màn tạo/sửa lệnh và
/// màn hoàn thành (sửa lại khi người làm thực tế khác kế hoạch).
class KhauSua {
  final int id;              // 0 = khâu mới, chưa lưu
  final String maKhau;
  final String tenKhau;
  final int thuTu;
  String? maCoSo;
  List<String> nguoi;
  String? ghiChu;

  KhauSua({
    this.id = 0,
    required this.maKhau,
    required this.tenKhau,
    required this.thuTu,
    this.maCoSo,
    List<String>? nguoi,
    this.ghiChu,
  }) : nguoi = nguoi ?? [];

  factory KhauSua.tuKhauLenh(KhauLenh k) => KhauSua(
        id: k.id,
        maKhau: k.maKhau,
        tenKhau: k.tenHienThi,
        thuTu: k.thuTu,
        maCoSo: k.maCoSo.isEmpty ? null : k.maCoSo,
        nguoi: [...k.nguoiThucHien],
        ghiChu: k.ghiChu,
      );

  /// Lỗi khi lập/sửa lệnh: người thực hiện KHÔNG bắt buộc (nhân viên sản xuất quét mã QR của lệnh để tham gia).
  String? get loiKhiLap => maCoSo == null ? 'chưa chọn cơ sở thực hiện' : null;

  /// Lỗi còn thiếu khi hoàn thành (phải có cơ sở và người thực hiện), null nếu đủ.
  String? get loi => maCoSo == null
      ? 'chưa chọn cơ sở thực hiện'
      : nguoi.isEmpty
          ? 'chưa có người thực hiện'
          : null;

  /// Thân gửi khi tạo/sửa lệnh.
  Map<String, dynamic> choLuu() =>
      {'maKhau': maKhau, 'thuTu': thuTu, 'maCoSo': maCoSo, 'nguoiThucHien': nguoi, 'ghiChu': ghiChu};

  /// Thân gửi khi sửa lại lúc hoàn thành (theo Id khâu đã lưu).
  Map<String, dynamic> choSuaLai() => {'id': id, 'maCoSo': maCoSo, 'nguoiThucHien': nguoi, 'ghiChu': ghiChu};
}

/// Dựng danh sách khâu theo quy trình; khâu trùng mã thì giữ thông tin đã nhập.
List<KhauSua> dungKhauTheoQuyTrinh(QuyTrinh qt, List<KhauSua> cu, {String? coSoMacDinh}) {
  final daNhap = {for (final k in cu) k.maKhau: k};
  final ds = [...qt.khau]..sort((a, b) => a.thuTu.compareTo(b.thuTu));
  return ds.map((k) {
    final c = daNhap[k.maKhau];
    return KhauSua(
      maKhau: k.maKhau,
      tenKhau: k.tenKhau ?? k.maKhau,
      thuTu: k.thuTu,
      maCoSo: c?.maCoSo ?? coSoMacDinh,
      nguoi: c == null ? [] : [...c.nguoi],
      ghiChu: c?.ghiChu,
    );
  }).toList();
}

/// Ô nhập một khâu: tên khâu, chọn cơ sở, chọn nhiều người thực hiện.
class OKhau extends ConsumerWidget {
  final KhauSua khau;
  final VoidCallback khiDoi;
  final bool khoa;

  /// Hoàn thành lệnh thì bắt buộc người thực hiện; lập lệnh thì không.
  final bool batBuocNguoi;

  const OKhau({super.key, required this.khau, required this.khiDoi, this.khoa = false, this.batBuocNguoi = true});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final coSo = ref.watch(coSoProvider).value ?? const <CoSo>[];
    final nhanSu = ref.watch(nhanSuProvider).value ?? const <NhanSu>[];
    final tenNguoi = {for (final n in nhanSu) n.maNhanSu: n.hoTen};
    final mauLoi = Theme.of(context).colorScheme.error;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${khau.thuTu}. ${khau.tenKhau}', style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          DropdownButtonFormField<String>(
            key: ValueKey('coso-${identityHashCode(khau)}-${khau.maCoSo}'),
            initialValue: coSo.any((c) => c.maCoSo == khau.maCoSo) ? khau.maCoSo : null,
            isExpanded: true,
            decoration: const InputDecoration(
                labelText: 'Cơ sở thực hiện *', border: OutlineInputBorder(), isDense: true),
            items: coSo.map((c) => DropdownMenuItem(value: c.maCoSo, child: Text(c.tenCoSo))).toList(),
            onChanged: khoa
                ? null
                : (v) {
                    khau.maCoSo = v;
                    khiDoi();
                  },
          ),
          const SizedBox(height: 8),
          InkWell(
            onTap: khoa ? null : () => _chonNguoi(context, nhanSu),
            child: InputDecorator(
              decoration: InputDecoration(
                labelText: batBuocNguoi ? 'Người thực hiện *' : 'Người thực hiện (không bắt buộc)',
                border: const OutlineInputBorder(),
                isDense: true,
                suffixIcon: const Icon(Icons.people_alt_outlined, size: 18),
                errorText: batBuocNguoi && khau.nguoi.isEmpty ? 'Chọn ít nhất 1 người' : null,
                errorStyle: TextStyle(color: mauLoi),
              ),
              child: khau.nguoi.isEmpty
                  ? const Text('—')
                  : Wrap(
                      spacing: 6,
                      runSpacing: 4,
                      children: khau.nguoi
                          .map((m) => Chip(
                                label: Text(tenNguoi[m] ?? m),
                                visualDensity: VisualDensity.compact,
                                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                              ))
                          .toList(),
                    ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _chonNguoi(BuildContext context, List<NhanSu> nhanSu) async {
    final chon = {...khau.nguoi};
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => StatefulBuilder(
        builder: (c, datLai) => AlertDialog(
          title: Text('Người thực hiện: ${khau.tenKhau}'),
          content: SizedBox(
            width: 380,
            child: nhanSu.isEmpty
                ? const Text('Chưa có nhân sự nào đang hoạt động.')
                : ListView(
                    shrinkWrap: true,
                    children: nhanSu
                        .map((n) => CheckboxListTile(
                              dense: true,
                              value: chon.contains(n.maNhanSu),
                              title: Text(n.hoTen),
                              subtitle: Text([n.maNhanSu, if (n.viTri != null) n.viTri!].join(' · ')),
                              onChanged: (v) => datLai(() => v == true ? chon.add(n.maNhanSu) : chon.remove(n.maNhanSu)),
                            ))
                        .toList(),
                  ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
            FilledButton(onPressed: () => Navigator.pop(c, true), child: Text('Chọn (${chon.length})')),
          ],
        ),
      ),
    );
    if (ok == true) {
      // Giữ thứ tự theo danh mục cho ổn định.
      khau.nguoi = nhanSu.map((n) => n.maNhanSu).where(chon.contains).toList();
      khiDoi();
    }
  }
}
