import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Một dòng đang nhập: giá trị theo khoá trường + hạng mục (checklist) + ghi chú.
class _DongNhap {
  final HangMucBieuMau? hangMuc;
  final Map<String, String> giaTri = {};
  String? ghiChu;
  _DongNhap({this.hangMuc});
}

/// Form nhập một phiếu ghi nhận theo biểu mẫu. Tự sinh widget theo kiểu từng trường.
class ManNhapPhieu extends ConsumerStatefulWidget {
  final BieuMau mau;
  const ManNhapPhieu({super.key, required this.mau});

  @override
  ConsumerState<ManNhapPhieu> createState() => _ManNhapPhieuState();
}

class _ManNhapPhieuState extends ConsumerState<ManNhapPhieu> {
  DateTime _ngay = DateTime.now();
  final _ca = TextEditingController();
  final _khuVuc = TextEditingController();
  final _ghiChu = TextEditingController();
  late List<_DongNhap> _dong;
  bool _dangLuu = false;

  @override
  void initState() {
    super.initState();
    _dong = _dungDongBanDau();
  }

  List<_DongNhap> _dungDongBanDau() {
    final m = widget.mau;
    if (m.laChecklist) return m.hangMuc.map((h) => _DongNhap(hangMuc: h)).toList();
    return [_DongNhap()]; // TheoNgay / NhieuDongTuDo bắt đầu 1 dòng
  }

  @override
  void dispose() {
    _ca.dispose();
    _khuVuc.dispose();
    _ghiChu.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // Tải sẵn danh mục cho các trường chọn.
    ref.watch(nhanSuBmProvider);
    ref.watch(coSoBmProvider);
    ref.watch(thanhPhamBmProvider);
    final m = widget.mau;

    return Scaffold(
      appBar: AppBar(title: Text(m.ten)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
        children: [
          Text(m.maHieu, style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 12),
          // Ngày + ca + khu vực
          Row(children: [
            Expanded(child: _oNgay()),
            const SizedBox(width: 12),
            Expanded(child: _oText(_ca, 'Ca / buổi', hint: 'vd Đầu ca, Sáng')),
          ]),
          const SizedBox(height: 12),
          _oText(_khuVuc, 'Khu vực / bộ phận', hint: 'vd khu sản xuất, biển số xe'),
          const SizedBox(height: 16),

          if (m.laChecklist)
            ..._dong.map(_theHangMuc)
          else if (m.laNhieuDong) ...[
            ..._dong.asMap().entries.map((e) => _theDongTuDo(e.key, e.value)),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: _dangLuu ? null : () => setState(() => _dong.add(_DongNhap())),
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Thêm dòng'),
              ),
            ),
          ] else
            _theTheoNgay(_dong.first),

          const SizedBox(height: 8),
          _oText(_ghiChu, 'Ghi chú phiếu', hint: null, lines: 2),

          if ((m.ghiChuChan ?? '').isNotEmpty) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.surfaceContainerHighest,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(children: [
                const Icon(Icons.info_outline, size: 18),
                const SizedBox(width: 8),
                Expanded(child: Text(m.ghiChuChan!, style: Theme.of(context).textTheme.bodySmall)),
              ]),
            ),
          ],
        ],
      ),
      bottomNavigationBar: Padding(
        padding: EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12),
        child: SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _dangLuu ? null : _luu,
            style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
            child: _dangLuu
                ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Lưu phiếu'),
          ),
        ),
      ),
    );
  }

  // ---------- Bố cục ----------

  Widget _theTheoNgay(_DongNhap dong) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: _theoNhom(dong),
      );

  Widget _theHangMuc(_DongNhap dong) {
    final h = dong.hangMuc!;
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(h.ten, style: const TextStyle(fontWeight: FontWeight.w600)),
            if ((h.dienGiai ?? '').isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(h.dienGiai!, style: Theme.of(context).textTheme.bodySmall),
              ),
            if ((h.tanSuat ?? '').isNotEmpty)
              Text('Tần suất: ${h.tanSuat}', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 8),
            ..._theoNhom(dong),
          ],
        ),
      ),
    );
  }

  Widget _theDongTuDo(int i, _DongNhap dong) => Card(
        margin: const EdgeInsets.only(bottom: 10),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(children: [
                Expanded(child: Text('Dòng ${i + 1}', style: const TextStyle(fontWeight: FontWeight.w600))),
                if (_dong.length > 1)
                  IconButton(
                    icon: const Icon(Icons.close, size: 18),
                    onPressed: _dangLuu ? null : () => setState(() => _dong.removeAt(i)),
                  ),
              ]),
              ..._theoNhom(dong),
            ],
          ),
        ),
      );

  /// Render các trường của một dòng, gom theo "Nhóm cột".
  List<Widget> _theoNhom(_DongNhap dong) {
    final w = <Widget>[];
    String? nhomHienTai;
    for (final t in widget.mau.truong) {
      if ((t.nhom ?? '') != (nhomHienTai ?? '') && (t.nhom ?? '').isNotEmpty) {
        nhomHienTai = t.nhom;
        w.add(Padding(
          padding: const EdgeInsets.only(top: 8, bottom: 2),
          child: Text(t.nhom!, style: TextStyle(fontWeight: FontWeight.w600, color: Theme.of(context).colorScheme.primary)),
        ));
      }
      w.add(Padding(padding: const EdgeInsets.symmetric(vertical: 4), child: _oTruong(dong, t)));
    }
    return w;
  }

  // ---------- Widget theo kiểu trường ----------

  Widget _oTruong(_DongNhap dong, TruongBieuMau t) {
    final nhan = t.ten + (t.batBuoc ? ' *' : '') + ((t.donVi ?? '').isNotEmpty ? ' (${t.donVi})' : '');
    final chuan = (t.giaTriChuan ?? '').isNotEmpty ? 'Chuẩn: ${t.giaTriChuan}' : null;
    switch (t.kieu) {
      case 'So':
        return _oNhap(dong, t, nhan, chuan, soL: true);
      case 'DatKhongDat':
        return _oDatKhongDat(dong, t, nhan);
      case 'Gio':
        return _oGio(dong, t, nhan);
      case 'Ngay':
        return _oNgayTruong(dong, t, nhan);
      case 'ChonNhanSu':
        return _oChon(dong, t, nhan, ref.watch(nhanSuBmProvider));
      case 'ChonCoSo':
        return _oChon(dong, t, nhan, ref.watch(coSoBmProvider));
      case 'ChonSanPham':
        return _oChon(dong, t, nhan, ref.watch(thanhPhamBmProvider));
      case 'LuaChon':
        return _oLuaChon(dong, t, nhan);
      default: // Text, ChonNcc (tạm nhập chữ), Anh (chưa hỗ trợ)
        return _oNhap(dong, t, nhan, chuan);
    }
  }

  Widget _oNhap(_DongNhap dong, TruongBieuMau t, String nhan, String? chuan, {bool soL = false}) =>
      TextFormField(
        initialValue: dong.giaTri[t.ma],
        keyboardType: soL ? const TextInputType.numberWithOptions(decimal: true, signed: true) : TextInputType.text,
        decoration: InputDecoration(labelText: nhan, helperText: chuan, border: const OutlineInputBorder(), isDense: true),
        onChanged: (v) => dong.giaTri[t.ma] = v,
      );

  Widget _oDatKhongDat(_DongNhap dong, TruongBieuMau t, String nhan) {
    final v = dong.giaTri[t.ma];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(nhan, style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: 2),
        Wrap(spacing: 8, children: [
          ChoiceChip(
            label: const Text('Đạt'),
            selected: v == 'Đạt',
            onSelected: _dangLuu ? null : (_) => setState(() => dong.giaTri[t.ma] = 'Đạt'),
          ),
          ChoiceChip(
            label: const Text('Không đạt'),
            selected: v == 'Không đạt',
            onSelected: _dangLuu ? null : (_) => setState(() => dong.giaTri[t.ma] = 'Không đạt'),
          ),
        ]),
      ],
    );
  }

  Widget _oGio(_DongNhap dong, TruongBieuMau t, String nhan) => _oBam(
        nhan: nhan,
        giaTri: dong.giaTri[t.ma],
        icon: Icons.schedule,
        onTap: () async {
          final now = TimeOfDay.now();
          final chon = await showTimePicker(context: context, initialTime: now);
          if (chon != null) {
            setState(() => dong.giaTri[t.ma] =
                '${chon.hour.toString().padLeft(2, '0')}:${chon.minute.toString().padLeft(2, '0')}');
          }
        },
      );

  Widget _oNgayTruong(_DongNhap dong, TruongBieuMau t, String nhan) => _oBam(
        nhan: nhan,
        giaTri: dong.giaTri[t.ma],
        icon: Icons.calendar_today,
        onTap: () async {
          final chon = await showDatePicker(
            context: context,
            initialDate: DateTime.now(),
            firstDate: DateTime(2020),
            lastDate: DateTime(2100),
          );
          if (chon != null) {
            setState(() => dong.giaTri[t.ma] =
                '${chon.year}-${chon.month.toString().padLeft(2, '0')}-${chon.day.toString().padLeft(2, '0')}');
          }
        },
      );

  Widget _oChon(_DongNhap dong, TruongBieuMau t, String nhan, AsyncValue<List<MucChon>> dm) {
    final ds = dm.value ?? const <MucChon>[];
    final hople = ds.any((m) => m.ma == dong.giaTri[t.ma]) ? dong.giaTri[t.ma] : null;
    return DropdownButtonFormField<String>(
      initialValue: hople,
      isExpanded: true,
      decoration: InputDecoration(labelText: nhan, border: const OutlineInputBorder(), isDense: true),
      items: ds.map((m) => DropdownMenuItem(value: m.ma, child: Text(m.ten, overflow: TextOverflow.ellipsis))).toList(),
      onChanged: _dangLuu ? null : (v) => setState(() => dong.giaTri[t.ma] = v ?? ''),
    );
  }

  Widget _oLuaChon(_DongNhap dong, TruongBieuMau t, String nhan) {
    final ds = t.tuyChon;
    final hople = ds.contains(dong.giaTri[t.ma]) ? dong.giaTri[t.ma] : null;
    return DropdownButtonFormField<String>(
      initialValue: hople,
      isExpanded: true,
      decoration: InputDecoration(labelText: nhan, border: const OutlineInputBorder(), isDense: true),
      items: ds.map((o) => DropdownMenuItem(value: o, child: Text(o))).toList(),
      onChanged: _dangLuu ? null : (v) => setState(() => dong.giaTri[t.ma] = v ?? ''),
    );
  }

  Widget _oBam({required String nhan, required String? giaTri, required IconData icon, required VoidCallback onTap}) =>
      InkWell(
        onTap: _dangLuu ? null : onTap,
        child: InputDecorator(
          decoration: InputDecoration(labelText: nhan, border: const OutlineInputBorder(), isDense: true,
              suffixIcon: Icon(icon, size: 18)),
          child: Text(giaTri ?? '—'),
        ),
      );

  Widget _oNgay() => InkWell(
        onTap: _dangLuu
            ? null
            : () async {
                final chon = await showDatePicker(
                  context: context,
                  initialDate: _ngay,
                  firstDate: DateTime(2020),
                  lastDate: DateTime(2100),
                );
                if (chon != null) setState(() => _ngay = chon);
              },
        child: InputDecorator(
          decoration: const InputDecoration(labelText: 'Ngày', border: OutlineInputBorder(), isDense: true,
              suffixIcon: Icon(Icons.calendar_today, size: 18)),
          child: Text('${_ngay.day.toString().padLeft(2, '0')}/${_ngay.month.toString().padLeft(2, '0')}/${_ngay.year}'),
        ),
      );

  Widget _oText(TextEditingController c, String nhan, {String? hint, int lines = 1}) => TextField(
        controller: c,
        maxLines: lines,
        decoration: InputDecoration(labelText: nhan, hintText: hint, border: const OutlineInputBorder(), isDense: true),
      );

  // ---------- Lưu ----------

  Future<void> _luu() async {
    // Kiểm tra trường bắt buộc từng dòng.
    final batBuoc = widget.mau.truong.where((t) => t.batBuoc).toList();
    for (final d in _dong) {
      for (final t in batBuoc) {
        if ((d.giaTri[t.ma] ?? '').trim().isEmpty) {
          final o = d.hangMuc != null ? ' (${d.hangMuc!.ten})' : '';
          _bao('Thiếu "${t.ten}"$o.');
          return;
        }
      }
    }

    // Với checklist/nhiều dòng, bỏ dòng hoàn toàn trống để khỏi gửi rác.
    final guiDong = _dong.where((d) => d.hangMuc != null || d.giaTri.values.any((v) => v.trim().isNotEmpty)).toList();
    if (guiDong.isEmpty) {
      _bao('Chưa nhập dữ liệu nào.');
      return;
    }

    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(khoBieuMauProvider).taoPhieu(
            bieuMauId: widget.mau.id,
            ngay: _ngay,
            ca: _ca.text.trim().isEmpty ? null : _ca.text.trim(),
            khuVuc: _khuVuc.text.trim().isEmpty ? null : _khuVuc.text.trim(),
            ghiChu: _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
            dong: guiDong
                .map((d) => {
                      'hangMucBieuMauId': d.hangMuc?.id,
                      'giaTri': d.giaTri,
                      'ghiChu': d.ghiChu,
                    })
                .toList(),
          );
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));
}
