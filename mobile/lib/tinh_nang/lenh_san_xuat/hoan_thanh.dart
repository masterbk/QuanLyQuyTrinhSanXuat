import 'dart:typed_data';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_image_compress/flutter_image_compress.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Ảnh đã chọn, giữ sẵn byte để gửi đi.
typedef AnhDaChon = ({String ten, Uint8List byte});

/// Hộp thoại hoàn thành lệnh: bắt buộc có ít nhất 1 ảnh lô thành phẩm.
class HopThoaiHoanThanh extends ConsumerStatefulWidget {
  final LenhSanXuat lenh;

  const HopThoaiHoanThanh({super.key, required this.lenh});

  @override
  ConsumerState<HopThoaiHoanThanh> createState() => _HopThoaiHoanThanhState();
}

class _HopThoaiHoanThanhState extends ConsumerState<HopThoaiHoanThanh> {
  final _anh = <AnhDaChon>[];
  bool _dangGui = false;

  Future<void> _themAnh(ImageSource nguon) async {
    try {
      final chon = ImagePicker();
      final files = nguon == ImageSource.camera
          ? [await chon.pickImage(source: ImageSource.camera, imageQuality: 85)]
          : await chon.pickMultiImage(imageQuality: 85);

      for (final f in files.whereType<XFile>()) {
        final goc = await f.readAsBytes();
        final byte = await _nen(goc);
        if (!mounted) return;
        setState(() => _anh.add((ten: f.name, byte: byte)));
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
    if (_anh.isEmpty) return _bao('Cần ít nhất 1 ảnh lô thành phẩm.');
    setState(() => _dangGui = true);
    try {
      final tb = await ref.read(khoLenhProvider).hoanThanh(widget.lenh.id, _anh);
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
    final l = widget.lenh;
    return AlertDialog(
      title: const Text('Hoàn thành lệnh'),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Hệ thống sẽ trừ nguyên liệu theo định mức và nhập '
                  '${soGon(l.soLuong)} thành phẩm vào lô ${l.maLoThanhPham}.'),
              const SizedBox(height: 16),
              Row(
                children: [
                  Text('Ảnh lô sản phẩm', style: Theme.of(context).textTheme.titleSmall),
                  Text(' *', style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ],
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  OutlinedButton.icon(
                    onPressed: _dangGui ? null : () => _themAnh(ImageSource.camera),
                    icon: const Icon(Icons.photo_camera_outlined, size: 18),
                    label: const Text('Chụp ảnh'),
                  ),
                  OutlinedButton.icon(
                    onPressed: _dangGui ? null : () => _themAnh(ImageSource.gallery),
                    icon: const Icon(Icons.photo_library_outlined, size: 18),
                    label: const Text('Chọn ảnh'),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (_anh.isEmpty)
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
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
                  children: _anh
                      .map((a) => Stack(
                            children: [
                              ClipRRect(
                                borderRadius: BorderRadius.circular(8),
                                child: Image.memory(a.byte, width: 96, height: 96, fit: BoxFit.cover),
                              ),
                              Positioned(
                                right: 0,
                                child: InkWell(
                                  onTap: _dangGui ? null : () => setState(() => _anh.remove(a)),
                                  child: Container(
                                    decoration: BoxDecoration(
                                        color: Colors.black54,
                                        borderRadius: BorderRadius.circular(12)),
                                    padding: const EdgeInsets.all(2),
                                    child: const Icon(Icons.close, size: 16, color: Colors.white),
                                  ),
                                ),
                              ),
                            ],
                          ))
                      .toList(),
                ),
              if (_anh.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text('${_anh.length} ảnh · ${_dungLuong()} sau khi nén',
                      style: Theme.of(context).textTheme.bodySmall),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: _dangGui ? null : () => Navigator.pop(context), child: const Text('Huỷ')),
        FilledButton(
          onPressed: (_dangGui || _anh.isEmpty) ? null : _gui,
          child: _dangGui
              ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Hoàn thành'),
        ),
      ],
    );
  }

  String _dungLuong() {
    final b = _anh.fold<int>(0, (t, a) => t + a.byte.length);
    return b < 1024 * 1024 ? '${(b / 1024).toStringAsFixed(0)} KB' : '${(b / 1048576).toStringAsFixed(1)} MB';
  }
}
