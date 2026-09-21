import 'dart:typed_data';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_image_compress/flutter_image_compress.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../loi/api.dart';
import '../lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon;
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Xác nhận đã giao: bắt buộc ít nhất 1 ảnh chụp tại chỗ làm bằng chứng.
class ManXacNhanGiao extends ConsumerStatefulWidget {
  final DonHangBan don;

  const ManXacNhanGiao({super.key, required this.don});

  @override
  ConsumerState<ManXacNhanGiao> createState() => _ManXacNhanGiaoState();
}

class _ManXacNhanGiaoState extends ConsumerState<ManXacNhanGiao> {
  static const _toiDa = 3;
  final _anh = <AnhDaChon>[];
  bool _dangGui = false;

  Future<void> _themAnh(ImageSource nguon) async {
    try {
      final chon = ImagePicker();
      final files = nguon == ImageSource.camera
          ? [await chon.pickImage(source: ImageSource.camera, imageQuality: 85)]
          : await chon.pickMultiImage(imageQuality: 85);

      for (final f in files.whereType<XFile>()) {
        if (_anh.length >= _toiDa) {
          if (mounted) _bao('Tối đa $_toiDa ảnh.');
          break;
        }
        final byte = await _nen(await f.readAsBytes());
        if (!mounted) return;
        setState(() => _anh.add((ten: f.name, byte: byte)));
      }
    } catch (e) {
      if (mounted) _bao('Không lấy được ảnh: $e');
    }
  }

  /// Ảnh điện thoại thường 3-5 MB; nén trước khi gửi cho đỡ tốn 4G (thư viện nén không chạy trên web).
  Future<Uint8List> _nen(Uint8List goc) async {
    if (kIsWeb) return goc;
    try {
      final nho = await FlutterImageCompress.compressWithList(goc, minWidth: 1600, minHeight: 1600, quality: 80);
      return nho.length < goc.length ? nho : goc;
    } catch (_) {
      return goc;
    }
  }

  Future<void> _gui() async {
    if (_anh.isEmpty) return _bao('Cần ít nhất 1 ảnh chứng minh đã giao.');
    setState(() => _dangGui = true);
    try {
      final tb = await ref.read(khoDonProvider).daGiao(widget.don.id, _anh);
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      if (mounted) _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));

  @override
  Widget build(BuildContext context) => PopScope(
        canPop: !_dangGui,
        child: Scaffold(
          appBar: AppBar(title: Text('Hoàn thành giao hàng ${widget.don.maDonHang}')),
          body: ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
            children: [
              Text(widget.don.tenKhachHang ?? '', style: Theme.of(context).textTheme.titleMedium),
              if ((widget.don.diaChiGiao ?? '').isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(widget.don.diaChiGiao!, style: Theme.of(context).textTheme.bodyMedium),
                ),
              const SizedBox(height: 16),
              Text('Ảnh giao hàng (bắt buộc, tối đa $_toiDa)', style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: 8),
              Row(
                children: [
                  FilledButton.tonalIcon(
                    onPressed: _dangGui ? null : () => _themAnh(ImageSource.camera),
                    icon: const Icon(Icons.photo_camera_outlined),
                    label: const Text('Chụp ảnh'),
                  ),
                  const SizedBox(width: 8),
                  OutlinedButton.icon(
                    onPressed: _dangGui ? null : () => _themAnh(ImageSource.gallery),
                    icon: const Icon(Icons.photo_library_outlined),
                    label: const Text('Chọn ảnh'),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final a in _anh)
                    Stack(
                      children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(8),
                          child: Image.memory(a.byte, width: 104, height: 104, fit: BoxFit.cover),
                        ),
                        Positioned(
                          right: 0,
                          child: IconButton(
                            icon: const CircleAvatar(radius: 12, child: Icon(Icons.close, size: 14)),
                            onPressed: _dangGui ? null : () => setState(() => _anh.remove(a)),
                          ),
                        ),
                      ],
                    ),
                ],
              ),
            ],
          ),
          bottomNavigationBar: SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: FilledButton.icon(
                onPressed: _dangGui || _anh.isEmpty ? null : _gui,
                style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                icon: const Icon(Icons.done_all),
                label: Text(_dangGui ? 'Đang gửi...' : 'Hoàn thành giao hàng'),
              ),
            ),
          ),
        ),
      );
}
