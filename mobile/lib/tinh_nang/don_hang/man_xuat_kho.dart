import 'dart:typed_data';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_image_compress/flutter_image_compress.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon, nhanSuProvider;
import '../lenh_san_xuat/mo_hinh.dart' show soGon;
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

class _OLo {
  final String maLo;
  final DateTime? hanSuDung;
  final double ton;
  final TextEditingController soLuong;

  _OLo({required this.maLo, this.hanSuDung, required this.ton, required double goiY})
      : soLuong = TextEditingController(text: soGon(goiY));

  double get soLuongSo => double.tryParse(soLuong.text.replaceAll(',', '.')) ?? 0;

  void dispose() => soLuong.dispose();
}

class _ODong {
  final int dongId;
  final String tenThanhPham;
  final String? donViTinh;
  final double soLuong;
  final List<_OLo> lo;

  _ODong({required this.dongId, required this.tenThanhPham, this.donViTinh, required this.soLuong,
      required this.lo});

  factory _ODong.tu(DongXuatKho d) => _ODong(
        dongId: d.dongId,
        tenThanhPham: d.tenThanhPham,
        donViTinh: d.donViTinh,
        soLuong: d.soLuong,
        lo: d.lo.map((l) => _OLo(maLo: l.maLo, hanSuDung: l.hanSuDung, ton: l.ton, goiY: l.goiY)).toList(),
      );

  double get daChon => lo.fold(0, (t, l) => t + l.soLuongSo);
}

/// Xuất kho: chọn lô (đã gợi ý FEFO, sửa được), người giao, ghi chú + ảnh tổng quan (đơn HanoiCheck).
/// Trừ tồn theo lô rồi chuyển đơn từ "Chờ xác nhận"/"Đã xác nhận" sang "Đang giao" (đã chọn người giao)
/// hoặc "Chờ giao hàng" (chưa chọn - chờ nhân viên tự nhận).
class ManXuatKho extends ConsumerStatefulWidget {
  final DonHangBan don;

  const ManXuatKho({super.key, required this.don});

  @override
  ConsumerState<ManXuatKho> createState() => _ManXuatKhoState();
}

class _ManXuatKhoState extends ConsumerState<ManXuatKho> {
  static const _toiDaAnh = 3;

  List<_ODong>? _dong;
  String? _loiTai;
  String? _maNguoiGiao;
  late final TextEditingController _ghiChu;
  final _anh = <AnhDaChon>[];
  bool _dangGui = false;
  bool _dangTaiAnh = false;

  bool get _guiHnC => widget.don.nguon == 'HanoiCheck';

  @override
  void initState() {
    super.initState();
    _maNguoiGiao = widget.don.maNguoiGiao;
    _ghiChu = TextEditingController(text: widget.don.ghiChu ?? '');
    _tai();
  }

  @override
  void dispose() {
    _ghiChu.dispose();
    _dong?.forEach((d) {
      for (final l in d.lo) {
        l.dispose();
      }
    });
    super.dispose();
  }

  Future<void> _tai() async {
    try {
      final ds = await ref.read(khoDonProvider).goiYXuatKho(widget.don.id);
      if (!mounted) return;
      setState(() => _dong = ds.map(_ODong.tu).toList());
    } on LoiApi catch (e) {
      if (mounted) setState(() => _loiTai = e.thongBao);
    }
  }

  Future<void> _themAnh(ImageSource nguon) async {
    setState(() => _dangTaiAnh = true);
    try {
      final chon = ImagePicker();
      final files = nguon == ImageSource.camera
          ? [await chon.pickImage(source: ImageSource.camera, imageQuality: 85)]
          : await chon.pickMultiImage(imageQuality: 85);

      for (final f in files.whereType<XFile>()) {
        if (_anh.length >= _toiDaAnh) {
          if (mounted) _bao('Tối đa $_toiDaAnh ảnh.');
          break;
        }
        final byte = await _nen(await f.readAsBytes());
        if (!mounted) return;
        setState(() => _anh.add((ten: f.name, byte: byte)));
      }
    } catch (e) {
      if (mounted) _bao('Không lấy được ảnh: $e');
    } finally {
      if (mounted) setState(() => _dangTaiAnh = false);
    }
  }

  Future<Uint8List> _nen(Uint8List goc) async {
    if (kIsWeb) return goc;
    try {
      final nho = await FlutterImageCompress.compressWithList(goc, minWidth: 1600, minHeight: 1600, quality: 80);
      return nho.length < goc.length ? nho : goc;
    } catch (_) {
      return goc;
    }
  }

  String? _kiemTra() {
    for (final d in _dong ?? const <_ODong>[]) {
      if (d.daChon != d.soLuong) {
        return '"${d.tenThanhPham}": đã chọn ${soGon(d.daChon)}, cần đúng ${soGon(d.soLuong)}.';
      }
    }
    return null;
  }

  Future<void> _xuatKho() async {
    final loi = _kiemTra();
    if (loi != null) return _bao(loi);

    setState(() => _dangGui = true);
    try {
      final phanBo = <PhanBoLo>[
        for (final d in _dong ?? const <_ODong>[])
          for (final l in d.lo)
            if (l.soLuongSo > 0) (dongId: d.dongId, maLo: l.maLo, soLuong: l.soLuongSo),
      ];
      final tb = await ref.read(khoDonProvider).xuatKho(widget.don.id, phanBo,
          maNguoiGiao: _maNguoiGiao, ghiChu: _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
          anh: _anh);
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));

  @override
  Widget build(BuildContext context) {
    final nguoiGiao = ref.watch(nhanSuProvider);

    return Scaffold(
      appBar: AppBar(title: Text('Xuất kho ${widget.don.maDonHang}')),
      body: _loiTai != null
          ? _Loi(thongBao: _loiTai!, thuLai: () {
              setState(() => _loiTai = null);
              _tai();
            })
          : _dong == null
              ? const Center(child: CircularProgressIndicator())
              : ListView(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
                  children: [
                    Text('Hệ thống đã gợi ý lô hết hạn trước (FEFO). Sửa số lượng nếu cần - mỗi dòng '
                        'phải xuất đúng số lượng đặt.'),
                    const SizedBox(height: 12),
                    nguoiGiao.when(
                      loading: () => const LinearProgressIndicator(),
                      error: (e, _) => _LoiNho('Lỗi tải nhân sự', () => ref.invalidate(nhanSuProvider)),
                      data: (ds) => DropdownButtonFormField<String>(
                        initialValue: ds.any((n) => n.maNhanSu == _maNguoiGiao) ? _maNguoiGiao : null,
                        isExpanded: true,
                        decoration: const InputDecoration(labelText: 'Người giao', border: OutlineInputBorder()),
                        items: [
                          const DropdownMenuItem(value: null, child: Text('(chưa chọn)')),
                          ...ds.map((n) => DropdownMenuItem(value: n.maNhanSu, child: Text(n.hoTen))),
                        ],
                        onChanged: (v) => setState(() => _maNguoiGiao = v),
                      ),
                    ),
                    const SizedBox(height: 14),
                    if (_guiHnC) ...[
                      TextFormField(
                        controller: _ghiChu,
                        maxLines: 2,
                        decoration: const InputDecoration(labelText: 'Ghi chú', border: OutlineInputBorder()),
                      ),
                      const SizedBox(height: 14),
                      Text('Ảnh tổng quan (tối đa $_toiDaAnh) - gửi kèm sang HanoiCheck',
                          style: Theme.of(context).textTheme.titleSmall),
                      const SizedBox(height: 8),
                      Row(
                        children: [
                          OutlinedButton.icon(
                            onPressed: _dangTaiAnh ? null : () => _themAnh(ImageSource.camera),
                            icon: const Icon(Icons.photo_camera_outlined, size: 18),
                            label: const Text('Chụp ảnh'),
                          ),
                          const SizedBox(width: 8),
                          OutlinedButton.icon(
                            onPressed: _dangTaiAnh ? null : () => _themAnh(ImageSource.gallery),
                            icon: const Icon(Icons.photo_library_outlined, size: 18),
                            label: const Text('Chọn ảnh'),
                          ),
                        ],
                      ),
                      if (_anh.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: _anh
                              .map((a) => Stack(
                                    children: [
                                      ClipRRect(
                                        borderRadius: BorderRadius.circular(8),
                                        child: Image.memory(a.byte, width: 88, height: 88, fit: BoxFit.cover),
                                      ),
                                      Positioned(
                                        right: 0,
                                        child: InkWell(
                                          onTap: () => setState(() => _anh.remove(a)),
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
                      ],
                      const SizedBox(height: 8),
                    ],
                    const Divider(height: 24),
                    for (final d in _dong!) _theDong(d),
                  ],
                ),
      bottomNavigationBar: _dong == null
          ? null
          : Padding(
              padding: EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12),
              child: FilledButton(
                onPressed: _dangGui ? null : _xuatKho,
                style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                child: _dangGui
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Xác nhận xuất kho'),
              ),
            ),
    );
  }

  Widget _theDong(_ODong d) => Card(
        margin: const EdgeInsets.only(bottom: 12),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text('${d.tenThanhPham} × ${soGon(d.soLuong)} ${d.donViTinh ?? ''}',
                        style: Theme.of(context).textTheme.titleSmall),
                  ),
                  Text('Đã chọn ${soGon(d.daChon)} / ${soGon(d.soLuong)}',
                      style: TextStyle(
                        color: d.daChon == d.soLuong ? Colors.green.shade700 : Theme.of(context).colorScheme.error,
                        fontWeight: FontWeight.w600,
                      )),
                ],
              ),
              if (d.lo.isEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text('Kho không còn lô nào của thành phẩm này.',
                      style: TextStyle(color: Theme.of(context).colorScheme.error)),
                )
              else
                for (final l in d.lo)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Row(
                      children: [
                        Expanded(
                          flex: 3,
                          child: Text(
                            'Lô ${l.maLo}${l.hanSuDung == null ? '' : ' · HSD ${_ngayVn.format(l.hanSuDung!)}'}'
                            '\nTồn ${soGon(l.ton)}',
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          flex: 2,
                          child: TextFormField(
                            controller: l.soLuong,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            decoration: const InputDecoration(labelText: 'Xuất', border: OutlineInputBorder()),
                            onChanged: (_) => setState(() {}),
                          ),
                        ),
                      ],
                    ),
                  ),
            ],
          ),
        ),
      );
}

class _LoiNho extends StatelessWidget {
  final String chu;
  final VoidCallback thuLai;

  const _LoiNho(this.chu, this.thuLai);

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Expanded(child: Text(chu, style: TextStyle(color: Theme.of(context).colorScheme.error))),
          TextButton(onPressed: thuLai, child: const Text('Thử lại')),
        ],
      );
}

class _Loi extends StatelessWidget {
  final String thongBao;
  final VoidCallback thuLai;

  const _Loi({required this.thongBao, required this.thuLai});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(thongBao, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: thuLai, child: const Text('Thử lại')),
            ],
          ),
        ),
      );
}
