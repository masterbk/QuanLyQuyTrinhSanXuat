import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:share_plus/share_plus.dart';

import '../../loi/api.dart';

/// Xem và lưu/chia sẻ ảnh QR (đơn hàng, lệnh sản xuất...) - dùng chung cho mọi loại, ảnh do máy chủ
/// tạo sẵn (đã kèm chữ mã bên dưới, giống ảnh QR trên web).
class ManXemQr extends StatefulWidget {
  final String tieuDe;
  final String tenFile;
  final Future<Uint8List> Function() taiAnh;

  const ManXemQr({super.key, required this.tieuDe, required this.tenFile, required this.taiAnh});

  @override
  State<ManXemQr> createState() => _ManXemQrState();
}

class _ManXemQrState extends State<ManXemQr> {
  late Future<Uint8List> _anh;
  bool _dangChiaSe = false;

  @override
  void initState() {
    super.initState();
    _anh = widget.taiAnh();
  }

  void _thuLai() => setState(() => _anh = widget.taiAnh());

  Future<void> _chiaSe(Uint8List bytes) async {
    setState(() => _dangChiaSe = true);
    try {
      await SharePlus.instance.share(ShareParams(
        files: [XFile.fromData(bytes, mimeType: 'image/png', name: widget.tenFile)],
        text: widget.tieuDe,
      ));
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Không lưu/chia sẻ được: $e')));
      }
    } finally {
      if (mounted) setState(() => _dangChiaSe = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(widget.tieuDe)),
        body: FutureBuilder<Uint8List>(
          future: _anh,
          builder: (context, snap) {
            if (snap.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snap.hasError) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        snap.error is LoiApi ? (snap.error as LoiApi).thongBao : 'Không tải được mã QR: ${snap.error}',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 12),
                      FilledButton.tonal(onPressed: _thuLai, child: const Text('Thử lại')),
                    ],
                  ),
                ),
              );
            }
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Card(
                  clipBehavior: Clip.antiAlias,
                  child: Image.memory(snap.data!, width: 280, fit: BoxFit.contain),
                ),
              ),
            );
          },
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: FutureBuilder<Uint8List>(
              future: _anh,
              builder: (context, snap) => FilledButton.icon(
                onPressed: (!snap.hasData || _dangChiaSe) ? null : () => _chiaSe(snap.data!),
                style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                icon: _dangChiaSe
                    ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.ios_share_outlined),
                label: const Text('Lưu / Chia sẻ'),
              ),
            ),
          ),
        ),
      );
}
