import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'man_chi_tiet.dart';
import 'mo_hinh.dart';

/// Nhân viên sản xuất quét mã QR của lệnh (hoặc gõ mã lệnh) rồi chọn các khâu mình tham gia.
class ManQuetMaLenh extends ConsumerStatefulWidget {
  const ManQuetMaLenh({super.key});

  @override
  ConsumerState<ManQuetMaLenh> createState() => _ManQuetMaLenhState();
}

class _ManQuetMaLenhState extends ConsumerState<ManQuetMaLenh> {
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

  Future<void> _moLenh(String? noiDung, {bool nhapTay = false}) async {
    if (_dangMo) return;
    final ma = maLenhTuQr(noiDung, nhapTay: nhapTay);
    if (ma == null) {
      setState(() => _loi = nhapTay ? 'Nhập mã lệnh, vd LSX-20260918-001.' : 'Đây không phải mã QR của lệnh sản xuất.');
      return;
    }

    setState(() {
      _dangMo = true;
      _loi = null;
    });
    try {
      final lenh = await ref.read(khoLenhProvider).theoMa(ma);
      if (!mounted) return;
      await _may.stop();
      if (!mounted) return;
      final xong = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => ManThamGia(lenh: lenh)));
      if (!mounted) return;
      if (xong == true) {
        Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => ManChiTietLenh(id: lenh.id)));
        return;
      }
      await _may.start();
    } on LoiApi catch (e) {
      if (mounted) setState(() => _loi = e.thongBao);
    } finally {
      if (mounted) setState(() => _dangMo = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Quét mã lệnh sản xuất')),
        body: Column(
          children: [
            Expanded(
              child: Stack(
                fit: StackFit.expand,
                children: [
                  MobileScanner(
                    controller: _may,
                    onDetect: (c) => _moLenh(c.barcodes.isEmpty ? null : c.barcodes.first.rawValue),
                    errorBuilder: (context, error) => Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Text('Không mở được camera (${error.errorCode.name}). Hãy nhập mã lệnh bên dưới.',
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
                            labelText: 'Hoặc nhập mã lệnh',
                            hintText: 'LSX-20260918-001',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                          onSubmitted: (v) => _moLenh(v, nhapTay: true),
                        ),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        onPressed: _dangMo ? null : () => _moLenh(_nhapTay.text, nhapTay: true),
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

/// Chọn các khâu mình thực hiện: chưa tham gia thì tick sẵn tất cả, đã tham gia thì giữ các khâu đang làm.
/// Bỏ chọn hết = rời lệnh.
class ManThamGia extends ConsumerStatefulWidget {
  final LenhSanXuat lenh;

  const ManThamGia({super.key, required this.lenh});

  @override
  ConsumerState<ManThamGia> createState() => _ManThamGiaState();
}

class _ManThamGiaState extends ConsumerState<ManThamGia> {
  late Set<int> _chon;
  late final bool _daThamGia;
  bool _dangGui = false;

  List<KhauLenh> get _tatCaKhau => [for (final s in widget.lenh.sanPham) ...s.khau];

  @override
  void initState() {
    super.initState();
    final ma = ref.read(xacThucProvider).nguoiDung?.maNhanSu ?? '';
    _chon = khauChonMacDinh(widget.lenh, ma);
    _daThamGia = _tatCaKhau.any((k) => k.nguoiThucHien.any((m) => m.toLowerCase() == ma.toLowerCase()));
  }

  Future<void> _gui() async {
    setState(() => _dangGui = true);
    try {
      final tb = await ref.read(khoLenhProvider).thamGia(widget.lenh.id, _chon.toList());
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      Navigator.pop(context, true);
    } on LoiApi catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = widget.lenh;
    final tatCa = _tatCaKhau;

    return Scaffold(
      appBar: AppBar(title: Text('Tham gia ${l.maLenh}')),
      body: !l.moiTao
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text('Lệnh đang ở trạng thái "${l.trangThaiHienThi}", không tham gia được nữa.',
                    textAlign: TextAlign.center),
              ),
            )
          : ListView(
              padding: const EdgeInsets.fromLTRB(8, 8, 8, 24),
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(8, 4, 8, 4),
                  child: Text('Chọn các khâu bạn thực hiện. Khâu nào bỏ chọn thì bạn không được ghi là người làm khâu đó.',
                      style: Theme.of(context).textTheme.bodyMedium),
                ),
                Row(
                  children: [
                    TextButton(
                        onPressed: () => setState(() => _chon = tatCa.map((k) => k.id).toSet()),
                        child: const Text('Chọn tất cả')),
                    TextButton(onPressed: () => setState(() => _chon = {}), child: const Text('Bỏ chọn hết')),
                  ],
                ),
                for (final s in l.sanPham) ...[
                  Padding(
                    padding: const EdgeInsets.fromLTRB(8, 12, 8, 4),
                    child: Text('${s.tenHienThi} ×${soGon(s.soLuong)} (lô ${s.maLoThanhPham})',
                        style: Theme.of(context).textTheme.titleSmall),
                  ),
                  for (final k in s.khau)
                    CheckboxListTile(
                      value: _chon.contains(k.id),
                      onChanged: (v) => setState(() => v == true ? _chon.add(k.id) : _chon.remove(k.id)),
                      title: Text('${k.thuTu}. ${k.tenHienThi}'),
                      subtitle: k.nguoiThucHien.isEmpty ? null : Text('Đang có ${k.nguoiThucHien.length} người'),
                    ),
                ],
              ],
            ),
      bottomNavigationBar: !l.moiTao
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: FilledButton.icon(
                  onPressed: _dangGui || (_chon.isEmpty && !_daThamGia) ? null : _gui,
                  style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                  icon: Icon(_chon.isEmpty ? Icons.logout : Icons.group_add),
                  label: Text(_chon.isEmpty ? 'Rời lệnh' : 'Tham gia ${_chon.length}/${tatCa.length} khâu'),
                ),
              ),
            ),
    );
  }
}
