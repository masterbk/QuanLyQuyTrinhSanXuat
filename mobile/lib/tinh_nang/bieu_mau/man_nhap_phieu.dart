import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
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

  /// Mở sẵn ngày này (mặc định hôm nay) - dùng khi "Nhập tiếp" một phiếu nháp từ màn Phiếu đã nhập.
  final DateTime? ngayBanDau;

  /// Mở đúng phiếu này (mẫu nhiều phiếu/ngày); mẫu 1 phiếu/ngày tự mở theo ngày nên không cần.
  final int? phieuId;

  const ManNhapPhieu({super.key, required this.mau, this.ngayBanDau, this.phieuId});

  @override
  ConsumerState<ManNhapPhieu> createState() => _ManNhapPhieuState();
}

class _ManNhapPhieuState extends ConsumerState<ManNhapPhieu> {
  late DateTime _ngay = widget.ngayBanDau ?? DateTime.now();
  bool _daMoPhieuChiDinh = false;
  final _ghiChu = TextEditingController();
  final _DongNhap _dauPhieu = _DongNhap(); // giá trị các trường đầu phiếu
  late List<_DongNhap> _dong;
  int? _phieuId; // phiếu đang mở (null = phiếu mới)
  DateTime? _mocLuu; // mốc lưu của phiếu lúc mở - gửi kèm khi cập nhật để không ghi đè người khác
  bool _choXem = false; // true = phiếu đã hoàn thành -> chỉ xem, khóa nhập
  List<PhieuGhiNhan> _dsNgay = []; // phiếu trong ngày (biểu mẫu nhiều phiếu/ngày)
  int _napLan = 0; // tăng mỗi lần nạp lại (đổi ngày/đổi phiếu) để ép các ô initialValue dựng lại
  bool _dangLuu = false;

  bool get _motPhieuNgay => widget.mau.motPhieuMoiNgay;
  bool get _khoaNhap => _dangLuu || _choXem; // khóa các thao tác nhập dữ liệu

  /// Khoá ô nhập: đổi khi nạp lại (ngày khác) hoặc khi dòng bị thay -> Flutter dựng ô mới, áp lại initialValue.
  Key _khoaO(_DongNhap dong, TruongBieuMau t) => ValueKey('${_napLan}_${identityHashCode(dong)}_${t.ma}');

  List<TruongBieuMau> get _truongDau => widget.mau.truong.where((t) => t.laDauPhieu).toList();
  List<TruongBieuMau> get _truongDong => widget.mau.truong.where((t) => !t.laDauPhieu).toList();

  @override
  void initState() {
    super.initState();
    _dong = _dungDongBanDau();
    Future.microtask(_napTheoNgay);
  }

  /// Nạp dữ liệu theo ngày đang chọn.
  /// - Mẫu 1 phiếu/ngày: lấy phiếu của ngày (nếu có) -> còn nháp thì sửa, đã hoàn thành thì xem.
  /// - Mẫu nhiều phiếu/ngày: lấy danh sách phiếu trong ngày; form bắt đầu là phiếu mới.
  Future<void> _napTheoNgay() async {
    try {
      final kho = ref.read(khoBieuMauProvider);
      if (_motPhieuNgay) {
        final p = await kho.phieuTheoNgay(widget.mau.id, _ngay);
        if (!mounted) return;
        setState(() => _apDungPhieu(p));
      } else {
        final ds = await kho.phieu(ngay: _ngay, bieuMauId: widget.mau.id);
        if (!mounted) return;
        // Lần nạp đầu mà được chỉ định phiếu -> mở luôn phiếu đó (nhập tiếp nháp).
        final chiDinh = _daMoPhieuChiDinh ? null : ds.where((p) => p.id == widget.phieuId).firstOrNull;
        _daMoPhieuChiDinh = true;
        setState(() { _dsNgay = ds; _apDungPhieu(chiDinh); });
      }
    } catch (_) {
      if (mounted) setState(() => _apDungPhieu(null)); // lỗi tải -> coi như phiếu mới
    }
  }

  /// Đưa một phiếu vào form. null = phiếu mới (trống). Phiếu đã hoàn thành -> chỉ xem (khóa nhập).
  void _apDungPhieu(PhieuGhiNhan? p) {
    _napLan++;
    _dong = _dungDongBanDau();
    _dauPhieu.giaTri.clear();
    _mocLuu = p?.thoiGianUtc;
    if (p == null) {
      _phieuId = null;
      _choXem = false;
      _ghiChu.text = '';
      return;
    }
    _phieuId = p.id;
    _choXem = p.laHoanThanh;
    _ghiChu.text = p.ghiChu ?? '';
    _dauPhieu.giaTri.addAll(p.giaTriDau);
    _apDungNhap(p);
  }

  /// Mở một phiếu trong danh sách ngày (mẫu nhiều phiếu/ngày): nháp -> sửa, đã hoàn thành -> xem.
  void _moPhieu(PhieuGhiNhan p) => setState(() => _apDungPhieu(p));

  /// Bắt đầu nhập một phiếu mới (mẫu nhiều phiếu/ngày).
  void _phieuMoi() => setState(() => _apDungPhieu(null));

  void _apDungNhap(PhieuGhiNhan nhap) {
    if (widget.mau.laChecklist) {
      final theoHangMuc = {for (final d in nhap.dong) d.hangMucId: d};
      for (final o in _dong) {
        final d = theoHangMuc[o.hangMuc?.id];
        if (d != null) { o.giaTri.addAll(d.giaTri); o.ghiChu = d.ghiChu; }
      }
    } else if (widget.mau.laNhieuDong) {
      _dong = nhap.dong.map((d) {
        final o = _DongNhap();
        o.giaTri.addAll(d.giaTri);
        o.ghiChu = d.ghiChu;
        return o;
      }).toList();
      if (_dong.isEmpty) _dong = [_DongNhap()];
    } else {
      // Theo ngày: một dòng.
      if (nhap.dong.isNotEmpty) {
        _dong.first.giaTri.addAll(nhap.dong.first.giaTri);
        _dong.first.ghiChu = nhap.dong.first.ghiChu;
      }
    }
  }

  /// Ghi giá trị một ô. Nhập dữ liệu (không phải ô chọn nhân sự) thì tự điền người đang đăng nhập vào các ô
  /// "Chọn nhân sự" CÒN TRỐNG cùng dòng và cùng nhóm cột - vd nhập nhiệt độ buổi sáng thì "Người kiểm tra" buổi
  /// sáng là mình, ô buổi chiều để nguyên cho người ca chiều. Trả true nếu có tự điền.
  bool _dat(_DongNhap dong, TruongBieuMau t, String v) {
    dong.giaTri[t.ma] = v;
    if (t.kieu == 'ChonNhanSu' || v.isEmpty) return false;
    final maToi = ref.read(xacThucProvider).nguoiDung?.maNhanSu;
    if (maToi == null || maToi.isEmpty) return false;
    final pham = identical(dong, _dauPhieu) ? _truongDau : _truongDong;
    var dien = false;
    for (final f in pham) {
      if (f.kieu == 'ChonNhanSu' && f.nhom == t.nhom && (dong.giaTri[f.ma] ?? '').isEmpty) {
        dong.giaTri[f.ma] = maToi;
        dien = true;
      }
    }
    return dien;
  }

  List<_DongNhap> _dungDongBanDau() {
    final m = widget.mau;
    if (m.laChecklist) return m.hangMuc.map((h) => _DongNhap(hangMuc: h)).toList();
    return [_DongNhap()]; // TheoNgay / NhieuDongTuDo bắt đầu 1 dòng
  }

  @override
  void dispose() {
    _ghiChu.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // Tải sẵn danh mục cho các trường chọn.
    ref.watch(nhanSuBmProvider);
    ref.watch(coSoBmProvider);
    ref.watch(thanhPhamBmProvider);
    ref.watch(nccBmProvider);
    final m = widget.mau;

    return Scaffold(
      appBar: AppBar(title: Text(m.ten)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
        children: [
          Text(m.maHieu, style: Theme.of(context).textTheme.bodySmall),
          _banner(),
          const SizedBox(height: 12),
          _oNgay(),
          if (!_motPhieuNgay) _dsNgaySection(),
          // Trường đầu phiếu (khu vực, biển số, tổ... - do biểu mẫu khai).
          for (final t in _truongDau)
            Padding(padding: const EdgeInsets.only(top: 12), child: _oTruong(_dauPhieu, t)),
          const SizedBox(height: 16),

          if (m.laChecklist)
            ..._dong.map(_theHangMuc)
          else if (m.laNhieuDong) ...[
            ..._dong.asMap().entries.map((e) => _theDongTuDo(e.key, e.value)),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: _khoaNhap ? null : () => setState(() => _dong.add(_DongNhap())),
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
      bottomNavigationBar: _thanhDuoi(context),
    );
  }

  // ---------- Banner + danh sách phiếu trong ngày + thanh nút ----------

  Widget _banner() {
    if (_choXem) {
      return _hopTin(Icons.lock_outline, 'Phiếu đã hoàn thành — chỉ xem, không sửa.',
          Theme.of(context).colorScheme.surfaceContainerHighest);
    }
    if (_phieuId != null) {
      return _hopTin(Icons.edit_note, 'Đang nhập tiếp phiếu nháp. Bấm "Hoàn thành" khi xong.',
          Theme.of(context).colorScheme.tertiaryContainer);
    }
    return const SizedBox.shrink();
  }

  Widget _hopTin(IconData icon, String text, Color mau) => Container(
        margin: const EdgeInsets.only(top: 8),
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(color: mau, borderRadius: BorderRadius.circular(8)),
        child: Row(children: [
          Icon(icon, size: 18),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: Theme.of(context).textTheme.bodySmall)),
        ]),
      );

  /// Danh sách phiếu đã nhập trong ngày (mẫu nhiều phiếu/ngày): chạm để mở (nháp sửa, hoàn thành xem).
  Widget _dsNgaySection() => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Expanded(child: Text('Phiếu trong ngày (${_dsNgay.length})',
                  style: const TextStyle(fontWeight: FontWeight.w600))),
              TextButton.icon(
                onPressed: _dangLuu ? null : _phieuMoi,
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Phiếu mới'),
              ),
            ]),
            if (_dsNgay.isEmpty)
              Text('Chưa có phiếu nào trong ngày.', style: Theme.of(context).textTheme.bodySmall)
            else
              ..._dsNgay.map((p) {
                final dangMo = p.id == _phieuId;
                return Card(
                  margin: const EdgeInsets.only(bottom: 6),
                  color: dangMo ? Theme.of(context).colorScheme.secondaryContainer : null,
                  child: ListTile(
                    dense: true,
                    leading: Icon(p.laHoanThanh ? Icons.check_circle : Icons.edit_note,
                        color: p.laHoanThanh ? Colors.green : null, size: 20),
                    title: Text('Phiếu #${p.id}'),
                    subtitle: Text(p.laHoanThanh ? 'Đã hoàn thành' : 'Nháp'),
                    trailing: dangMo ? const Text('Đang mở') : const Icon(Icons.chevron_right, size: 18),
                    onTap: _dangLuu ? null : () => _moPhieu(p),
                  ),
                );
              }),
            const Divider(height: 24),
          ],
        ),
      );

  Widget _thanhDuoi(BuildContext context) {
    final pad = EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12);
    if (_choXem) {
      return Padding(
        padding: pad,
        child: Row(children: [
          Expanded(
            child: OutlinedButton(
              onPressed: () => Navigator.pop(context),
              style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
              child: const Text('Đóng'),
            ),
          ),
          if (!_motPhieuNgay) ...[
            const SizedBox(width: 12),
            Expanded(
              child: FilledButton.icon(
                onPressed: _phieuMoi,
                style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
                icon: const Icon(Icons.add),
                label: const Text('Phiếu mới'),
              ),
            ),
          ],
        ]),
      );
    }
    return Padding(
      padding: pad,
      child: Row(children: [
        Expanded(
          child: OutlinedButton(
            onPressed: _dangLuu ? null : () => _luu(false),
            style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
            child: const Text('Lưu nháp'),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: FilledButton(
            onPressed: _dangLuu ? null : () => _luu(true),
            style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
            child: _dangLuu
                ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Hoàn thành'),
          ),
        ),
      ]),
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
                    onPressed: _khoaNhap ? null : () => setState(() => _dong.removeAt(i)),
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
    for (final t in _truongDong) {
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
      case 'ChonNcc':
        return _oChon(dong, t, nhan, ref.watch(nccBmProvider));
      case 'LuaChon':
        return _oLuaChon(dong, t, nhan);
      case 'Anh':
        return _oAnh(dong, t, nhan);
      default: // Text
        return _oNhap(dong, t, nhan, chuan);
    }
  }

  Widget _oAnh(_DongNhap dong, TruongBieuMau t, String nhan) {
    final url = dong.giaTri[t.ma];
    final coAnh = url != null && url.isNotEmpty;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(nhan, style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: 4),
        if (coAnh)
          Stack(children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.network(url, height: 120, width: 120, fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => Container(
                      height: 120, width: 120, color: Colors.black12, child: const Icon(Icons.broken_image))),
            ),
            if (!_choXem)
              Positioned(
                right: -8, top: -8,
                child: IconButton(
                  icon: const Icon(Icons.cancel, color: Colors.red),
                  onPressed: _khoaNhap ? null : () => setState(() => dong.giaTri.remove(t.ma)),
                ),
              ),
          ])
        else if (_choXem)
          Text('(không có ảnh)', style: Theme.of(context).textTheme.bodySmall)
        else
          OutlinedButton.icon(
            onPressed: _khoaNhap ? null : () => _chonAnh(dong, t),
            icon: const Icon(Icons.add_a_photo, size: 18),
            label: const Text('Chụp / chọn ảnh'),
          ),
      ],
    );
  }

  Future<void> _chonAnh(_DongNhap dong, TruongBieuMau t) async {
    final nguon = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (c) => SafeArea(
        child: Wrap(children: [
          ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Chụp ảnh'),
              onTap: () => Navigator.pop(c, ImageSource.camera)),
          ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Chọn từ thư viện'),
              onTap: () => Navigator.pop(c, ImageSource.gallery)),
        ]),
      ),
    );
    if (nguon == null) return;
    final x = await ImagePicker().pickImage(source: nguon, imageQuality: 85, maxWidth: 1600);
    if (x == null) return;
    setState(() => _dangLuu = true); // tạm khóa trong lúc tải ảnh
    try {
      final bytes = await x.readAsBytes();
      final url = await ref.read(khoBieuMauProvider).taiAnh(bytes, x.name);
      if (!mounted) return;
      setState(() => _dat(dong, t, url));
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } catch (_) {
      _bao('Không tải được ảnh. Vui lòng thử lại.');
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  Widget _oNhap(_DongNhap dong, TruongBieuMau t, String nhan, String? chuan, {bool soL = false}) =>
      TextFormField(
        key: _khoaO(dong, t),
        initialValue: dong.giaTri[t.ma],
        enabled: !_choXem,
        keyboardType: soL ? const TextInputType.numberWithOptions(decimal: true, signed: true) : TextInputType.text,
        decoration: InputDecoration(labelText: nhan, helperText: chuan, border: const OutlineInputBorder(), isDense: true),
        onChanged: (v) {
          if (_dat(dong, t, v)) setState(() {}); // chỉ dựng lại khi vừa tự điền người
        },
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
            onSelected: _khoaNhap ? null : (_) => setState(() => _dat(dong, t, 'Đạt')),
          ),
          ChoiceChip(
            label: const Text('Không đạt'),
            selected: v == 'Không đạt',
            onSelected: _khoaNhap ? null : (_) => setState(() => _dat(dong, t, 'Không đạt')),
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
            setState(() => _dat(dong, t,
                '${chon.hour.toString().padLeft(2, '0')}:${chon.minute.toString().padLeft(2, '0')}'));
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
            setState(() => _dat(dong, t,
                '${chon.year}-${chon.month.toString().padLeft(2, '0')}-${chon.day.toString().padLeft(2, '0')}'));
          }
        },
      );

  Widget _oChon(_DongNhap dong, TruongBieuMau t, String nhan, AsyncValue<List<MucChon>> dm) {
    final ds = dm.value ?? const <MucChon>[];
    final hople = ds.any((m) => m.ma == dong.giaTri[t.ma]) ? dong.giaTri[t.ma] : null;
    return DropdownButtonFormField<String>(
      key: ValueKey('${_napLan}_${identityHashCode(dong)}_${t.ma}_$hople'),
      initialValue: hople,
      isExpanded: true,
      decoration: InputDecoration(labelText: nhan, border: const OutlineInputBorder(), isDense: true),
      items: ds.map((m) => DropdownMenuItem(value: m.ma, child: Text(m.ten, overflow: TextOverflow.ellipsis))).toList(),
      onChanged: _khoaNhap ? null : (v) => setState(() => _dat(dong, t, v ?? '')),
    );
  }

  Widget _oLuaChon(_DongNhap dong, TruongBieuMau t, String nhan) {
    final ds = t.tuyChon;
    final hople = ds.contains(dong.giaTri[t.ma]) ? dong.giaTri[t.ma] : null;
    return DropdownButtonFormField<String>(
      key: _khoaO(dong, t),
      initialValue: hople,
      isExpanded: true,
      decoration: InputDecoration(labelText: nhan, border: const OutlineInputBorder(), isDense: true),
      items: ds.map((o) => DropdownMenuItem(value: o, child: Text(o))).toList(),
      onChanged: _khoaNhap ? null : (v) => setState(() => _dat(dong, t, v ?? '')),
    );
  }

  Widget _oBam({required String nhan, required String? giaTri, required IconData icon, required VoidCallback onTap}) =>
      InkWell(
        onTap: _khoaNhap ? null : onTap,
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
                if (chon != null && chon != _ngay) {
                  setState(() => _ngay = chon);
                  await _napTheoNgay(); // nạp phiếu của ngày mới chọn
                }
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
        enabled: !_choXem,
        decoration: InputDecoration(labelText: nhan, hintText: hint, border: const OutlineInputBorder(), isDense: true),
      );

  // ---------- Lưu ----------

  Future<void> _luu(bool hoanThanh) async {
    if (_choXem) return; // đang xem phiếu đã hoàn thành
    // Trường bắt buộc chỉ bắt khi Hoàn thành (Lưu nháp cho phép thiếu).
    if (hoanThanh) {
      for (final t in _truongDau.where((t) => t.batBuoc)) {
        if ((_dauPhieu.giaTri[t.ma] ?? '').trim().isEmpty) { _bao('Thiếu "${t.ten}".'); return; }
      }
      final batBuoc = _truongDong.where((t) => t.batBuoc).toList();
      for (final d in _dong) {
        for (final t in batBuoc) {
          if ((d.giaTri[t.ma] ?? '').trim().isEmpty) {
            final o = d.hangMuc != null ? ' (${d.hangMuc!.ten})' : '';
            _bao('Thiếu "${t.ten}"$o.');
            return;
          }
        }
      }
    }

    // Với checklist giữ mọi hạng mục; nhiều dòng/theo ngày bỏ dòng hoàn toàn trống.
    final guiDong = _dong.where((d) => d.hangMuc != null || d.giaTri.values.any((v) => v.trim().isNotEmpty)).toList();
    if (guiDong.isEmpty) {
      _bao('Chưa nhập dữ liệu nào.');
      return;
    }

    final giaTriDau = Map<String, String>.fromEntries(
        _dauPhieu.giaTri.entries.where((e) => e.value.trim().isNotEmpty));
    final dongGui = guiDong
        .map((d) => {'hangMucBieuMauId': d.hangMuc?.id, 'giaTri': d.giaTri, 'ghiChu': d.ghiChu})
        .toList();
    final ghiChu = _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim();

    // Xác nhận trước khi chốt (hoàn thành rồi sẽ khóa, không sửa tiếp được).
    if (hoanThanh && !await _xacNhanHoanThanh()) return;

    setState(() => _dangLuu = true);
    try {
      final kho = ref.read(khoBieuMauProvider);
      final String tb;
      if (_phieuId != null) {
        tb = await kho.capNhatPhieu(id: _phieuId!, mocLuu: _mocLuu, bieuMauId: widget.mau.id, ngay: _ngay,
            giaTriDau: giaTriDau, ghiChu: ghiChu, dong: dongGui, hoanThanh: hoanThanh);
      } else {
        tb = await kho.taoPhieu(bieuMauId: widget.mau.id, ngay: _ngay,
            giaTriDau: giaTriDau, ghiChu: ghiChu, dong: dongGui, hoanThanh: hoanThanh);
      }
      if (!mounted) return;
      if (hoanThanh) {
        if (_motPhieuNgay) {
          Navigator.pop(context, tb); // 1 phiếu/ngày: về lại danh sách biểu mẫu
          return;
        }
        // Nhiều phiếu/ngày: ở lại, làm mới danh sách và reset form để nhập phiếu kế tiếp.
        _bao(tb);
        await _napTheoNgay();
      } else {
        _bao(tb);
        // Đảm bảo có id để lần lưu sau nhập tiếp đúng phiếu.
        // Đảm bảo có id + mốc lưu MỚI để lần lưu sau nhập tiếp đúng phiếu và không bị coi là ghi đè.
        if (_motPhieuNgay) {
          final p = await kho.phieuTheoNgay(widget.mau.id, _ngay);
          if (mounted) setState(() { _phieuId = p?.id; _mocLuu = p?.thoiGianUtc; });
        } else {
          final ds = await kho.phieu(ngay: _ngay, bieuMauId: widget.mau.id);
          if (mounted) {
            setState(() {
              _dsNgay = ds;
              _phieuId ??= ds.isNotEmpty ? ds.first.id : null;
              _mocLuu = ds.where((x) => x.id == _phieuId).firstOrNull?.thoiGianUtc;
            });
          }
        }
      }
    } on LoiApi catch (e) {
      if (e.maHttp == 409) {
        await _baoXungDot(e.thongBao);
      } else {
        _bao(e.thongBao);
      }
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  /// Phiếu vừa bị người khác lưu: không ghi đè; cho tải lại bản mới nhất (bỏ phần vừa nhập trên máy này).
  Future<void> _baoXungDot(String thongBao) async {
    final taiLai = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Phiếu đã thay đổi'),
        content: Text('$thongBao\n\nTải lại sẽ bỏ phần bạn vừa nhập trên máy này để hiện bản mới nhất.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Để sau')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Tải lại')),
        ],
      ),
    );
    if (taiLai != true || !mounted) return;
    final id = _phieuId;
    _daMoPhieuChiDinh = false;
    await _napTheoNgay();
    // Mẫu nhiều phiếu/ngày: mở lại đúng phiếu đang sửa.
    final p = _dsNgay.where((x) => x.id == id).firstOrNull;
    if (!_motPhieuNgay && p != null && mounted) setState(() => _apDungPhieu(p));
  }

  /// Hỏi xác nhận trước khi chốt phiếu. Trả true nếu người dùng đồng ý.
  Future<bool> _xacNhanHoanThanh() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Hoàn thành phiếu?'),
        content: Text(_motPhieuNgay
            ? 'Sau khi hoàn thành, phiếu của ngày này sẽ được chốt và chỉ xem lại được (không sửa tiếp). Tiếp tục?'
            : 'Chốt và lưu phiếu này? Sau khi hoàn thành chỉ xem lại được.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Hoàn thành')),
        ],
      ),
    );
    return ok ?? false;
  }

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));
}
