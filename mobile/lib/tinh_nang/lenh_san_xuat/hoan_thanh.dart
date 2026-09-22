import 'dart:typed_data';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_image_compress/flutter_image_compress.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';
import 'o_khau.dart';

/// Màn hoàn thành lệnh: MỖI sản phẩm (lô) bắt buộc có ít nhất 1 ảnh; người thực hiện từng khâu
/// được sửa lại nếu thực tế khác kế hoạch. Xong thì pop về thông báo của máy chủ.
class ManHoanThanh extends ConsumerStatefulWidget {
  final LenhSanXuat lenh;

  const ManHoanThanh({super.key, required this.lenh});

  @override
  ConsumerState<ManHoanThanh> createState() => _ManHoanThanhState();
}

class _ManHoanThanhState extends ConsumerState<ManHoanThanh> {
  late final Map<int, List<AnhDaChon>> _anh = {for (final s in widget.lenh.sanPham) s.id: <AnhDaChon>[]};
  late final Map<int, List<KhauSua>> _khau = {
    for (final s in widget.lenh.sanPham)
      s.id: ([...s.khau]..sort((a, b) => a.thuTu.compareTo(b.thuTu))).map(KhauSua.tuKhauLenh).toList()
  };
  // Hạn dùng lô nhập ở bước hoàn thành; mặc định 3 ngày kể từ ngày sản xuất.
  late final Map<int, DateTime> _hanSuDung = {
    for (final s in widget.lenh.sanPham)
      s.id: s.hanSuDung ?? widget.lenh.ngaySanXuat.add(const Duration(days: 3))
  };
  bool _dangGui = false;

  /// Album ảnh của Lô sản xuất gửi HanoiCheck: tối đa 3 ảnh mỗi lô.
  static const _soAnhLoHnC = 3;

  /// Máy chủ nhận tối đa 30 ảnh cho một lần hoàn thành lệnh.
  static const _tongAnhToiDa = 30;

  bool get _duAnh => _anh.values.every((ds) => ds.isNotEmpty);

  Future<void> _themAnh(int idSanPham, ImageSource nguon) async {
    // Chưa tải xong công tắc thì áp giới hạn chặt (như khi gửi HanoiCheck) cho an toàn.
    final guiHanoiCheck = widget.lenh.taoLoDongBo &&
        ref.read(hanoiCheckBatProvider).maybeWhen(data: (v) => v, orElse: () => true);
    try {
      final chon = ImagePicker();
      final files = nguon == ImageSource.camera
          ? [await chon.pickImage(source: ImageSource.camera, imageQuality: 85)]
          : await chon.pickMultiImage(imageQuality: 85);

      for (final f in files.whereType<XFile>()) {
        if (guiHanoiCheck && _anh[idSanPham]!.length >= _soAnhLoHnC) {
          if (mounted) _bao('Lô gửi HanoiCheck tối đa $_soAnhLoHnC ảnh.');
          break;
        }
        if (_anh.values.fold<int>(0, (t, ds) => t + ds.length) >= _tongAnhToiDa) {
          if (mounted) _bao('Tối đa $_tongAnhToiDa ảnh cho cả lệnh.');
          break;
        }
        final byte = await _nen(await f.readAsBytes());
        if (!mounted) return;
        setState(() => _anh[idSanPham]!.add((ten: f.name, byte: byte)));
      }
    } catch (e) {
      if (mounted) _bao('Không lấy được ảnh: $e');
    }
  }

  /// Ảnh điện thoại thường 3-5 MB; nén trước khi gửi để đỡ tốn 4G và nhẹ máy chủ.
  /// Thư viện nén không chạy trên web nên bản web gửi nguyên ảnh (chỉ dùng lúc phát triển).
  Future<Uint8List> _nen(Uint8List goc) async {
    if (kIsWeb) return goc;
    try {
      final nho = await FlutterImageCompress.compressWithList(goc,
          minWidth: 1600, minHeight: 1600, quality: 80);
      return nho.length < goc.length ? nho : goc;
    } catch (_) {
      return goc;
    }
  }

  Future<void> _gui() async {
    for (final s in widget.lenh.sanPham) {
      if (_anh[s.id]!.isEmpty) return _bao('Cần ít nhất 1 ảnh cho lô ${s.maLoThanhPham}.');
      for (final k in _khau[s.id]!) {
        final loi = k.loi;
        if (loi != null) return _bao('${s.tenHienThi} - khâu "${k.tenKhau}": $loi.');
      }
    }

    setState(() => _dangGui = true);
    try {
      final khau = _khau.values.expand((ds) => ds).map((k) => k.choSuaLai()).toList();
      final tb = await ref.read(khoLenhProvider)
          .hoanThanh(widget.lenh.id, _anh, khau, hanSuDung: _hanSuDung);
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));

  String _ngayVn(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';

  Future<void> _chonHanSuDung(int idSanPham) async {
    final chon = await showDatePicker(
      context: context,
      initialDate: _hanSuDung[idSanPham]!,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
    );
    if (chon != null && mounted) setState(() => _hanSuDung[idSanPham] = chon);
  }

  @override
  Widget build(BuildContext context) {
    ref.watch(coSoProvider);
    ref.watch(nhanSuProvider);
    ref.watch(hanoiCheckBatProvider); // tải sẵn công tắc HanoiCheck cho giới hạn ảnh
    final l = widget.lenh;
    final tongAnh = _anh.values.fold<int>(0, (t, ds) => t + ds.length);

    return PopScope(
      canPop: !_dangGui,
      child: Scaffold(
        appBar: AppBar(title: Text('Hoàn thành ${l.maLenh}')),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
          children: [
            Text('Hệ thống sẽ trừ nguyên liệu theo định mức (cộng dồn ${l.sanPham.length} sản phẩm) '
                'và nhập thành phẩm vào kho. Mỗi lô cần ít nhất 1 ảnh.'),
            const SizedBox(height: 12),
            for (final s in l.sanPham) _theSanPham(s),
          ],
        ),
        bottomNavigationBar: Padding(
          padding: EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (tongAnh > 0)
                Padding(
                  padding: const EdgeInsets.only(bottom: 6),
                  child: Text('$tongAnh ảnh · ${_dungLuong()} sau khi nén',
                      style: Theme.of(context).textTheme.bodySmall),
                ),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: (_dangGui || !_duAnh) ? null : _gui,
                  style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                  child: _dangGui
                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('Hoàn thành'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _theSanPham(SanPhamLenh s) {
    final anh = _anh[s.id]!;
    final khau = _khau[s.id]!;
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${s.tenHienThi} ×${soGon(s.soLuong)}',
                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            Text('Lô ${s.maLoThanhPham}', style: TextStyle(color: Theme.of(context).hintColor)),
            const SizedBox(height: 8),
            Row(
              children: [
                const Icon(Icons.event_outlined, size: 18),
                const SizedBox(width: 6),
                Text('Hạn dùng: ${_ngayVn(_hanSuDung[s.id]!)}'),
                const Spacer(),
                TextButton(
                  onPressed: _dangGui ? null : () => _chonHanSuDung(s.id),
                  child: const Text('Đổi'),
                ),
              ],
            ),
            const SizedBox(height: 2),
            Row(
              children: [
                Text('Ảnh lô sản phẩm', style: Theme.of(context).textTheme.titleSmall),
                Text(' *', style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ],
            ),
            const SizedBox(height: 6),
            Wrap(
              spacing: 8,
              children: [
                OutlinedButton.icon(
                  onPressed: _dangGui ? null : () => _themAnh(s.id, ImageSource.camera),
                  icon: const Icon(Icons.photo_camera_outlined, size: 18),
                  label: const Text('Chụp ảnh'),
                ),
                OutlinedButton.icon(
                  onPressed: _dangGui ? null : () => _themAnh(s.id, ImageSource.gallery),
                  icon: const Icon(Icons.photo_library_outlined, size: 18),
                  label: const Text('Chọn ảnh'),
                ),
              ],
            ),
            const SizedBox(height: 8),
            if (anh.isEmpty)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.errorContainer,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Text('Chưa có ảnh nào.'),
              )
            else
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: anh
                    .map((a) => Stack(
                          children: [
                            ClipRRect(
                              borderRadius: BorderRadius.circular(8),
                              child: Image.memory(a.byte, width: 88, height: 88, fit: BoxFit.cover),
                            ),
                            Positioned(
                              right: 0,
                              child: InkWell(
                                onTap: _dangGui ? null : () => setState(() => anh.remove(a)),
                                child: Container(
                                  decoration: BoxDecoration(
                                      color: Colors.black54, borderRadius: BorderRadius.circular(12)),
                                  padding: const EdgeInsets.all(2),
                                  child: const Icon(Icons.close, size: 16, color: Colors.white),
                                ),
                              ),
                            ),
                          ],
                        ))
                    .toList(),
              ),
            if (khau.isNotEmpty)
              ExpansionTile(
                tilePadding: EdgeInsets.zero,
                title: Text('Người thực hiện (${khau.length} khâu)'),
                subtitle: const Text('Sửa nếu thực tế khác kế hoạch'),
                children: [
                  if (khau.length > 1)
                    OGanNguoiMoiKhau(khau: khau, khoa: _dangGui, khiDoi: () => setState(() {})),
                  for (final k in khau)
                    OKhau(key: ObjectKey(k), khau: k, khoa: _dangGui, khiDoi: () => setState(() {})),
                ],
              ),
          ],
        ),
      ),
    );
  }

  String _dungLuong() {
    final b = _anh.values.expand((ds) => ds).fold<int>(0, (t, a) => t + a.byte.length);
    return b < 1024 * 1024 ? '${(b / 1024).toStringAsFixed(0)} KB' : '${(b / 1048576).toStringAsFixed(1)} MB';
  }
}
