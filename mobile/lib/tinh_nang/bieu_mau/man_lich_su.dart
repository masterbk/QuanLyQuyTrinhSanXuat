import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import 'kho_du_lieu.dart';
import 'man_chi_tiet_phieu.dart';
import 'mo_hinh.dart';

/// Khoảng thời gian lọc danh sách phiếu.
enum KhoangNgay {
  bayNgay('7 ngày'),
  baMuoiNgay('30 ngày'),
  thangNay('Tháng này'),
  tatCa('Tất cả');

  final String nhan;
  const KhoangNgay(this.nhan);

  /// Ngày bắt đầu của khoảng (null = không giới hạn), tính theo giờ Việt Nam.
  DateTime? tuNgay(DateTime homNay) => switch (this) {
        KhoangNgay.bayNgay => homNay.subtract(const Duration(days: 6)),
        KhoangNgay.baMuoiNgay => homNay.subtract(const Duration(days: 29)),
        KhoangNgay.thangNay => DateTime(homNay.year, homNay.month, 1),
        KhoangNgay.tatCa => null,
      };
}

/// Nhãn nhóm ngày: "Hôm nay", "Hôm qua" hoặc dd/MM/yyyy.
String nhanNgay(DateTime ngay, DateTime homNay) {
  final d = DateTime(ngay.year, ngay.month, ngay.day);
  final h = DateTime(homNay.year, homNay.month, homNay.day);
  final lech = h.difference(d).inDays;
  if (lech == 0) return 'Hôm nay';
  if (lech == 1) return 'Hôm qua';
  return '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';
}

/// Danh sách phiếu đã nhập (mới nhất trước), nhóm theo ngày, tải thêm khi cuộn tới cuối.
/// [bieuMauId] lọc sẵn theo một mẫu (mở từ nút "Lịch sử" của mẫu); [nhung] = nằm trong tab, không có AppBar.
class ManLichSuPhieu extends ConsumerStatefulWidget {
  final int? bieuMauId;
  final bool nhung;
  const ManLichSuPhieu({super.key, this.bieuMauId, this.nhung = false});

  @override
  ConsumerState<ManLichSuPhieu> createState() => _ManLichSuPhieuState();
}

class _ManLichSuPhieuState extends ConsumerState<ManLichSuPhieu> {
  static const _soDong = 20;

  bool _cuaToi = false;
  KhoangNgay _khoang = KhoangNgay.bayNgay;
  late int? _bieuMauId = widget.bieuMauId;

  final List<PhieuTomTat> _ds = [];
  int _trang = 0;
  bool _conTrang = true;
  bool _dangTai = false;
  String? _loi;
  final _cuon = ScrollController();

  @override
  void initState() {
    super.initState();
    _cuon.addListener(() {
      if (_cuon.position.pixels > _cuon.position.maxScrollExtent - 300) _taiThem();
    });
    Future.microtask(_taiLai);
  }

  @override
  void dispose() {
    _cuon.dispose();
    super.dispose();
  }

  Future<void> _taiLai() async {
    setState(() {
      _ds.clear();
      _trang = 0;
      _conTrang = true;
      _loi = null;
    });
    await _taiThem();
  }

  Future<void> _taiThem() async {
    if (_dangTai || !_conTrang) return;
    setState(() => _dangTai = true);
    try {
      final homNay = bayGioVietNam();
      final kq = await ref.read(khoBieuMauProvider).lichSu(
            tuNgay: _khoang.tuNgay(homNay),
            bieuMauId: _bieuMauId,
            cuaToi: _cuaToi,
            trang: _trang + 1,
            soDong: _soDong,
          );
      if (!mounted) return;
      setState(() {
        _ds.addAll(kq.duLieu);
        _trang = kq.trang;
        _conTrang = kq.conTrangSau;
      });
    } on LoiApi catch (e) {
      if (mounted) setState(() => _loi = e.thongBao);
    } catch (e) {
      if (mounted) setState(() => _loi = 'Không tải được danh sách phiếu: $e');
    } finally {
      if (mounted) setState(() => _dangTai = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final noiDung = Column(children: [
      _thanhLoc(),
      const Divider(height: 1),
      Expanded(child: _danhSach()),
    ]);
    if (widget.nhung) return noiDung;
    return Scaffold(appBar: AppBar(title: const Text('Phiếu đã nhập')), body: noiDung);
  }

  Widget _thanhLoc() {
    final dsMau = ref.watch(bieuMauProvider).value ?? const <BieuMau>[];
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
      child: Column(children: [
        Row(children: [
          FilterChip(
            label: const Text('Do tôi lập'),
            selected: _cuaToi,
            onSelected: (v) {
              setState(() => _cuaToi = v);
              _taiLai();
            },
          ),
          const Spacer(),
          DropdownButton<KhoangNgay>(
            value: _khoang,
            underline: const SizedBox.shrink(),
            items: KhoangNgay.values.map((k) => DropdownMenuItem(value: k, child: Text(k.nhan))).toList(),
            onChanged: (k) {
              if (k == null) return;
              setState(() => _khoang = k);
              _taiLai();
            },
          ),
        ]),
        DropdownButtonFormField<int?>(
          initialValue: dsMau.any((m) => m.id == _bieuMauId) ? _bieuMauId : null,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'Biểu mẫu', isDense: true, border: OutlineInputBorder()),
          items: [
            const DropdownMenuItem<int?>(value: null, child: Text('Tất cả biểu mẫu')),
            ...dsMau.map((m) => DropdownMenuItem<int?>(
                value: m.id, child: Text(m.ten, overflow: TextOverflow.ellipsis))),
          ],
          onChanged: (v) {
            setState(() => _bieuMauId = v);
            _taiLai();
          },
        ),
      ]),
    );
  }

  Widget _danhSach() {
    if (_ds.isEmpty) {
      if (_dangTai) return const Center(child: CircularProgressIndicator());
      return RefreshIndicator(
        onRefresh: _taiLai,
        child: ListView(physics: const AlwaysScrollableScrollPhysics(), children: [
          const SizedBox(height: 80),
          Icon(_loi == null ? Icons.inbox_outlined : Icons.error_outline, size: 48, color: Colors.grey),
          const SizedBox(height: 8),
          Text(_loi ?? 'Chưa có phiếu nào trong khoảng thời gian này.', textAlign: TextAlign.center),
          if (_loi != null) Center(child: TextButton(onPressed: _taiLai, child: const Text('Thử lại'))),
        ]),
      );
    }

    // Phẳng hoá thành [tiêu đề ngày, phiếu, phiếu, tiêu đề ngày, ...].
    final homNay = bayGioVietNam();
    final muc = <Object>[];
    String? nhomTruoc;
    for (final p in _ds) {
      final nhom = nhanNgay(p.ngay, homNay);
      if (nhom != nhomTruoc) muc.add(nhom);
      nhomTruoc = nhom;
      muc.add(p);
    }
    final tenNguoi = {for (final n in ref.watch(nhanSuBmProvider).value ?? const <MucChon>[]) n.ma: n.ten};

    return RefreshIndicator(
      onRefresh: _taiLai,
      child: ListView.builder(
        controller: _cuon,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
        itemCount: muc.length + 1,
        itemBuilder: (context, i) {
          if (i == muc.length) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: Center(
                child: _dangTai
                    ? const CircularProgressIndicator()
                    : Text(_conTrang ? '' : 'Đã hiện hết ${_ds.length} phiếu',
                        style: Theme.of(context).textTheme.bodySmall),
              ),
            );
          }
          final m = muc[i];
          if (m is String) {
            return Padding(
              padding: const EdgeInsets.fromLTRB(4, 12, 4, 6),
              child: Text(m.toUpperCase(),
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(fontWeight: FontWeight.bold)),
            );
          }
          final p = m as PhieuTomTat;
          return Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: ThePhieu(
              phieu: p,
              tenNguoiLap: p.tenNguoiLap ?? tenNguoi[p.nguoiLap] ?? p.nguoiLap,
              onTap: () async {
                await Navigator.push(context, MaterialPageRoute(builder: (_) => ManChiTietPhieu(phieuId: p.id)));
                if (mounted) _taiLai(); // có thể vừa "Nhập tiếp" xong
              },
            ),
          );
        },
      ),
    );
  }
}

/// Thẻ một phiếu: tên mẫu + trạng thái, đầu phiếu, tóm tắt Đạt/Không đạt, người lập + giờ.
class ThePhieu extends StatelessWidget {
  final PhieuTomTat phieu;
  final String? tenNguoiLap;
  final VoidCallback onTap;
  const ThePhieu({super.key, required this.phieu, this.tenNguoiLap, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final p = phieu;
    final nho = Theme.of(context).textTheme.bodySmall;
    final gio = gioVietNam(p.thoiGianUtc);
    final coDatKd = p.soDat + p.soKhongDat > 0;
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Expanded(
                child: Text(p.tenMau, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
              ),
              const SizedBox(width: 8),
              ChipTrangThaiPhieu(laNhap: p.laNhap),
            ]),
            const SizedBox(height: 2),
            Text([p.maHieu, ...p.dauPhieu].join(' · '), style: nho),
            const SizedBox(height: 6),
            if (coDatKd)
              Text.rich(TextSpan(children: [
                TextSpan(text: '${p.soDat} đạt'),
                if (p.soKhongDat > 0)
                  TextSpan(
                      text: ' · ${p.soKhongDat} KHÔNG ĐẠT',
                      style: TextStyle(color: Colors.red.shade700, fontWeight: FontWeight.bold)),
              ]))
            else
              Text('${p.soDong} dòng'),
            const SizedBox(height: 2),
            Text('${tenNguoiLap ?? 'Không rõ người lập'} · '
                '${gio.hour.toString().padLeft(2, '0')}:${gio.minute.toString().padLeft(2, '0')}',
                style: nho),
          ]),
        ),
      ),
    );
  }
}

/// Chip trạng thái phiếu: Nháp (cam) / Đã ghi nhận (xanh).
class ChipTrangThaiPhieu extends StatelessWidget {
  final bool laNhap;
  const ChipTrangThaiPhieu({super.key, required this.laNhap});

  @override
  Widget build(BuildContext context) {
    final (bg, fg, nhan) = laNhap
        ? (Colors.orange.shade100, Colors.orange.shade900, 'Nháp')
        : (Colors.green.shade100, Colors.green.shade900, 'Đã ghi nhận');
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(20)),
      child: Text(nhan, style: TextStyle(fontSize: 12, color: fg, fontWeight: FontWeight.w600)),
    );
  }
}
