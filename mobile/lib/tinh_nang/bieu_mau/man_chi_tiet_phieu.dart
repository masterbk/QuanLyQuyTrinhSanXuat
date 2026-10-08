import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:printing/printing.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_lich_su.dart' show ChipTrangThaiPhieu;
import 'man_nhap_phieu.dart';
import 'mo_hinh.dart';

/// Có ô Đạt/Không đạt nào của dòng là "Không đạt" không.
bool dongCoKhongDat(DongGhiNhan? d, List<TruongBieuMau> truong) =>
    d != null &&
    truong.any((t) => t.kieu == 'DatKhongDat' && (d.giaTri[t.ma] ?? '').isNotEmpty && d.giaTri[t.ma] != 'Đạt');

/// Xem chi tiết một phiếu (chỉ xem): mỗi dòng là một thẻ "nhãn: giá trị" thay cho bảng ngang của bản in.
/// Phiếu nháp có nút "Nhập tiếp"; nút PDF mở bản in để in / chia sẻ.
class ManChiTietPhieu extends ConsumerStatefulWidget {
  final int phieuId;
  const ManChiTietPhieu({super.key, required this.phieuId});

  @override
  ConsumerState<ManChiTietPhieu> createState() => _ManChiTietPhieuState();
}

class _ManChiTietPhieuState extends ConsumerState<ManChiTietPhieu> {
  PhieuGhiNhan? _phieu;
  BieuMau? _mau;
  String? _loi;
  bool _chiKhongDat = false;
  bool _dangPdf = false;

  @override
  void initState() {
    super.initState();
    Future.microtask(_tai);
  }

  Future<void> _tai() async {
    setState(() => _loi = null);
    try {
      final kho = ref.read(khoBieuMauProvider);
      final p = await kho.chiTietPhieu(widget.phieuId);
      final dsNhap = ref.read(bieuMauProvider).value ?? const <BieuMau>[];
      final m = dsNhap.where((x) => x.id == p.bieuMauId).firstOrNull ?? await kho.bieuMauTheoId(p.bieuMauId);
      if (mounted) setState(() { _phieu = p; _mau = m; });
    } on LoiApi catch (e) {
      if (mounted) setState(() => _loi = e.thongBao);
    } catch (e) {
      if (mounted) setState(() => _loi = 'Không tải được phiếu: $e');
    }
  }

  Future<void> _moPdf() async {
    setState(() => _dangPdf = true);
    try {
      final bytes = await ref.read(khoBieuMauProvider).pdfPhieu(widget.phieuId);
      await Printing.layoutPdf(onLayout: (_) async => bytes, name: 'Phieu-${widget.phieuId}.pdf');
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } catch (_) {
      _bao('Không mở được PDF. Vui lòng thử lại.');
    } finally {
      if (mounted) setState(() => _dangPdf = false);
    }
  }

  void _bao(String s) {
    if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));
  }

  @override
  Widget build(BuildContext context) {
    final p = _phieu, m = _mau;
    // Chỉ "Nhập tiếp" khi phiếu còn nháp VÀ mẫu đang nằm trong danh sách mình được nhập.
    final duocNhap = (ref.watch(bieuMauProvider).value ?? const <BieuMau>[]).any((x) => x.id == p?.bieuMauId);
    return Scaffold(
      appBar: AppBar(
        title: Text(m?.ten ?? 'Phiếu ghi nhận'),
        actions: [
          IconButton(
            tooltip: 'Xem PDF / In / Chia sẻ',
            onPressed: p == null || _dangPdf ? null : _moPdf,
            icon: _dangPdf
                ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.picture_as_pdf),
          ),
        ],
      ),
      body: _loi != null
          ? Center(
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                Text(_loi!, textAlign: TextAlign.center),
                TextButton(onPressed: _tai, child: const Text('Thử lại')),
              ]),
            )
          : p == null || m == null
              ? const Center(child: CircularProgressIndicator())
              : RefreshIndicator(onRefresh: _tai, child: _noiDung(p, m)),
      bottomNavigationBar: p != null && m != null && !p.laHoanThanh && duocNhap
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                child: FilledButton.icon(
                  icon: const Icon(Icons.edit_note),
                  label: const Text('Nhập tiếp'),
                  onPressed: () async {
                    final tb = await Navigator.push<String>(context, MaterialPageRoute(
                        builder: (_) => ManNhapPhieu(mau: m, ngayBanDau: p.ngay, phieuId: p.id)));
                    if (tb != null) _bao(tb);
                    await _tai();
                  },
                ),
              ),
            )
          : null,
    );
  }

  Widget _noiDung(PhieuGhiNhan p, BieuMau m) {
    final truongDau = m.truong.where((t) => t.laDauPhieu).toList();
    final truongDong = m.truong.where((t) => !t.laDauPhieu).toList();
    final nho = Theme.of(context).textTheme.bodySmall;
    final ngay = '${p.ngay.day.toString().padLeft(2, '0')}/${p.ngay.month.toString().padLeft(2, '0')}/${p.ngay.year}';

    // Dòng theo bố cục: checklist theo thứ tự hạng mục (kể cả hạng mục chưa nhập); còn lại theo thứ tự dòng.
    final List<({String? tieuDe, DongGhiNhan? dong})> dsDong;
    if (m.laChecklist) {
      final theoHm = {for (final d in p.dong) d.hangMucId: d};
      dsDong = m.hangMuc.map((h) => (tieuDe: h.ten as String?, dong: theoHm[h.id])).toList();
    } else {
      final sx = [...p.dong]..sort((a, b) => a.thuTu.compareTo(b.thuTu));
      dsDong = [
        for (var i = 0; i < sx.length; i++) (tieuDe: m.laNhieuDong ? 'Dòng ${i + 1}' : null, dong: sx[i] as DongGhiNhan?)
      ];
    }
    final soKhongDat = dsDong.where((x) => dongCoKhongDat(x.dong, truongDong)).length;
    final hienThi = _chiKhongDat ? dsDong.where((x) => dongCoKhongDat(x.dong, truongDong)).toList() : dsDong;

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
      children: [
        Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Expanded(child: Text('${m.maHieu} · $ngay', style: nho)),
                ChipTrangThaiPhieu(laNhap: !p.laHoanThanh),
              ]),
              const SizedBox(height: 6),
              _DongGiaTri(nhan: 'Người lập', giaTri: Text(p.tenNguoiLap ?? _ten(nhanSuBmProvider, p.nguoiLap) ?? '—')),
              for (final t in truongDau) _DongGiaTri(nhan: t.ten, giaTri: _giaTri(t, p.giaTriDau[t.ma])),
              if ((p.ghiChu ?? '').isNotEmpty) _DongGiaTri(nhan: 'Ghi chú', giaTri: Text(p.ghiChu!)),
            ]),
          ),
        ),
        const SizedBox(height: 12),
        if (m.laChecklist)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Wrap(spacing: 8, children: [
              ChoiceChip(
                label: Text('Tất cả (${dsDong.length})'),
                selected: !_chiKhongDat,
                onSelected: (_) => setState(() => _chiKhongDat = false),
              ),
              ChoiceChip(
                label: Text('Không đạt ($soKhongDat)'),
                selected: _chiKhongDat,
                onSelected: (_) => setState(() => _chiKhongDat = true),
              ),
            ]),
          ),
        if (hienThi.isEmpty)
          Padding(
            padding: const EdgeInsets.all(24),
            child: Text(_chiKhongDat ? 'Không có hạng mục nào không đạt.' : 'Phiếu chưa có dòng nào.',
                textAlign: TextAlign.center),
          ),
        for (final x in hienThi)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: _theDong(x.tieuDe, x.dong, truongDong),
          ),
      ],
    );
  }

  Widget _theDong(String? tieuDe, DongGhiNhan? d, List<TruongBieuMau> truong) {
    final khongDat = dongCoKhongDat(d, truong);
    final con = <Widget>[];
    if (tieuDe != null) {
      con.add(Text(tieuDe, style: const TextStyle(fontWeight: FontWeight.bold)));
      con.add(const SizedBox(height: 4));
    }
    if (d == null) {
      con.add(Text('Chưa nhập', style: Theme.of(context).textTheme.bodySmall));
    } else {
      String? nhomTruoc;
      for (final t in truong) {
        if ((t.nhom ?? '').isNotEmpty && t.nhom != nhomTruoc) {
          con.add(Padding(
            padding: const EdgeInsets.only(top: 6, bottom: 2),
            child: Text(t.nhom!, style: Theme.of(context).textTheme.labelMedium?.copyWith(fontWeight: FontWeight.bold)),
          ));
        }
        nhomTruoc = t.nhom;
        con.add(_DongGiaTri(nhan: t.donVi == null ? t.ten : '${t.ten} (${t.donVi})', giaTri: _giaTri(t, d.giaTri[t.ma])));
      }
      if ((d.ghiChu ?? '').isNotEmpty) con.add(_DongGiaTri(nhan: 'Ghi chú', giaTri: Text(d.ghiChu!)));
    }
    return Card(
      margin: EdgeInsets.zero,
      shape: khongDat
          ? RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12), side: BorderSide(color: Colors.red.shade300))
          : null,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: con),
      ),
    );
  }

  String? _ten(FutureProvider<List<MucChon>> dm, String? ma) {
    if (ma == null || ma.isEmpty) return null;
    final ds = ref.watch(dm).value ?? const <MucChon>[];
    return ds.where((x) => x.ma == ma).firstOrNull?.ten ?? ma;
  }

  /// Hiển thị giá trị theo kiểu trường: Đạt/KĐ thành chip màu, ảnh thành ảnh thu nhỏ, mã danh mục thành tên.
  Widget _giaTri(TruongBieuMau t, String? v) {
    if (v == null || v.isEmpty) return Text('—', style: TextStyle(color: Theme.of(context).disabledColor));
    switch (t.kieu) {
      case 'DatKhongDat':
        final dat = v == 'Đạt';
        return Align(
          alignment: Alignment.centerLeft,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
            decoration: BoxDecoration(
                color: dat ? Colors.green.shade100 : Colors.red.shade100, borderRadius: BorderRadius.circular(20)),
            child: Text(v,
                style: TextStyle(
                    color: dat ? Colors.green.shade900 : Colors.red.shade900, fontWeight: FontWeight.w600)),
          ),
        );
      case 'Anh':
        return Align(
          alignment: Alignment.centerLeft,
          child: GestureDetector(
            onTap: () => showDialog(
              context: context,
              builder: (_) => Dialog(child: InteractiveViewer(child: Image.network(v))),
            ),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.network(v, height: 90, width: 90, fit: BoxFit.cover,
                  errorBuilder: (_, _, _) =>
                      Container(height: 90, width: 90, color: Colors.black12, child: const Icon(Icons.broken_image))),
            ),
          ),
        );
      case 'ChonNhanSu':
        return Text(_ten(nhanSuBmProvider, v) ?? v);
      case 'ChonCoSo':
        return Text(_ten(coSoBmProvider, v) ?? v);
      case 'ChonSanPham':
        return Text(_ten(thanhPhamBmProvider, v) ?? v);
      case 'ChonNcc':
        return Text(_ten(nccBmProvider, v) ?? v);
      case 'Ngay':
        final d = DateTime.tryParse(v);
        return Text(d == null ? v : '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}');
      default:
        return Text(v);
    }
  }
}

/// Một dòng "nhãn: giá trị" (nhãn bên trái, giá trị bên phải).
class _DongGiaTri extends StatelessWidget {
  final String nhan;
  final Widget giaTri;
  const _DongGiaTri({required this.nhan, required this.giaTri});

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Expanded(flex: 2, child: Text(nhan, style: Theme.of(context).textTheme.bodySmall)),
          const SizedBox(width: 8),
          Expanded(flex: 3, child: giaTri),
        ]),
      );
}
