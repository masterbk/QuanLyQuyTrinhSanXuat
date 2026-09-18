import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_chi_tiet.dart';

/// Quét mã QR trên phiếu giao của đơn (QR tra cứu của hệ thống hoặc link truy xuất HanoiCheck) để mở đúng đơn.
class ManQuetDonHang extends ConsumerStatefulWidget {
  const ManQuetDonHang({super.key});

  @override
  ConsumerState<ManQuetDonHang> createState() => _ManQuetDonHangState();
}

class _ManQuetDonHangState extends ConsumerState<ManQuetDonHang> {
  final _may = MobileScannerController(formats: const [BarcodeFormat.qrCode]);
  final _nhapTay = TextEditingController();
  bool _dangMo = false;
  String? _loi;

  @override
  void dispose() {
    _may.dispose();
    _nhapTay.dispose();
    super.dispose();
  }

  Future<void> _moDon(String? noiDung) async {
    final s = (noiDung ?? '').trim();
    if (_dangMo || s.isEmpty) return;

    setState(() {
      _dangMo = true;
      _loi = null;
    });
    try {
      final don = await ref.read(khoDonProvider).quet(s);
      if (!mounted) return;
      await _may.stop();
      if (!mounted) return;
      await Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => ManChiTietDon(id: don.id)));
    } on LoiApi catch (e) {
      if (mounted) setState(() => _loi = e.thongBao);
    } finally {
      if (mounted) setState(() => _dangMo = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Quét QR đơn hàng')),
        body: Column(
          children: [
            Expanded(
              child: Stack(
                fit: StackFit.expand,
                children: [
                  MobileScanner(
                    controller: _may,
                    onDetect: (c) => _moDon(c.barcodes.isEmpty ? null : c.barcodes.first.rawValue),
                    errorBuilder: (context, error) => Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Text('Không mở được camera (${error.errorCode.name}). Hãy nhập mã đơn bên dưới.',
                            textAlign: TextAlign.center, style: const TextStyle(color: Colors.white)),
                      ),
                    ),
                  ),
                  if (_dangMo)
                    const ColoredBox(color: Colors.black38, child: Center(child: CircularProgressIndicator())),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_loi != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Text(_loi!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                    ),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _nhapTay,
                          textCapitalization: TextCapitalization.characters,
                          decoration: const InputDecoration(
                            labelText: 'Hoặc nhập mã đơn',
                            hintText: 'DH-20260918-001',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                          onSubmitted: _moDon,
                        ),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        onPressed: _dangMo ? null : () => _moDon(_nhapTay.text),
                        child: const Text('Mở'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      );
}
