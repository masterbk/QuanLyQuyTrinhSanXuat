import 'package:flutter/material.dart';

import 'mo_hinh.dart';

const _coDau = 'àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ';
const _khongDau = 'aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd';

/// Chữ thường, bỏ dấu tiếng Việt - để tìm "nghia" ra "Nghĩa".
String boDau(String s) {
  final b = StringBuffer();
  for (final c in s.toLowerCase().split('')) {
    final i = _coDau.indexOf(c);
    b.write(i < 0 ? c : _khongDau[i]);
  }
  return b.toString();
}

/// Lọc danh mục theo tên hoặc mã, không phân biệt hoa thường và dấu.
List<MucChon> locMucChon(List<MucChon> ds, String tuKhoa) {
  final k = boDau(tuKhoa.trim());
  if (k.isEmpty) return ds;
  return ds.where((m) => boDau(m.ten).contains(k) || boDau(m.ma).contains(k)).toList();
}

/// Ô chọn có tìm kiếm (thay dropdown): chạm để mở danh sách có ô gõ tìm, chọn "Bỏ chọn" để xoá.
/// [onChanged] null = chỉ xem.
class OChonTimKiem extends StatelessWidget {
  final String nhan;
  final String? giaTri;
  final List<MucChon> ds;
  final ValueChanged<String>? onChanged;

  const OChonTimKiem({super.key, required this.nhan, required this.giaTri, required this.ds, this.onChanged});

  @override
  Widget build(BuildContext context) {
    final ten = (giaTri ?? '').isEmpty ? null : (ds.where((m) => m.ma == giaTri).firstOrNull?.ten ?? giaTri);
    return InkWell(
      onTap: onChanged == null
          ? null
          : () async {
              // null = đóng không chọn; '' = bỏ chọn.
              final kq = await showModalBottomSheet<String>(
                context: context,
                isScrollControlled: true,
                builder: (_) => _BangChon(tieuDe: nhan, ds: ds, dangChon: giaTri),
              );
              if (kq != null) onChanged!(kq);
            },
      child: InputDecorator(
        isEmpty: ten == null,
        decoration: InputDecoration(
          labelText: nhan,
          border: const OutlineInputBorder(),
          isDense: true,
          enabled: onChanged != null,
          suffixIcon: const Icon(Icons.arrow_drop_down),
        ),
        child: Text(ten ?? '', overflow: TextOverflow.ellipsis),
      ),
    );
  }
}

class _BangChon extends StatefulWidget {
  final String tieuDe;
  final List<MucChon> ds;
  final String? dangChon;
  const _BangChon({required this.tieuDe, required this.ds, this.dangChon});

  @override
  State<_BangChon> createState() => _BangChonState();
}

class _BangChonState extends State<_BangChon> {
  String _tuKhoa = '';

  @override
  Widget build(BuildContext context) {
    final loc = locMucChon(widget.ds, _tuKhoa);
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: SizedBox(
          height: MediaQuery.of(context).size.height * 0.75,
          child: Column(children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
              child: TextField(
                autofocus: true,
                decoration: InputDecoration(
                  labelText: 'Tìm ${widget.tieuDe.toLowerCase()}',
                  prefixIcon: const Icon(Icons.search),
                  border: const OutlineInputBorder(),
                  isDense: true,
                ),
                onChanged: (v) => setState(() => _tuKhoa = v),
              ),
            ),
            Expanded(
              child: ListView(children: [
                if ((widget.dangChon ?? '').isNotEmpty)
                  ListTile(
                    leading: const Icon(Icons.clear),
                    title: const Text('Bỏ chọn'),
                    onTap: () => Navigator.pop(context, ''),
                  ),
                if (loc.isEmpty)
                  const Padding(padding: EdgeInsets.all(24), child: Center(child: Text('Không tìm thấy.'))),
                for (final m in loc)
                  ListTile(
                    title: Text(m.ten),
                    subtitle: m.ma != m.ten ? Text(m.ma) : null,
                    trailing: m.ma == widget.dangChon ? const Icon(Icons.check, color: Colors.green) : null,
                    onTap: () => Navigator.pop(context, m.ma),
                  ),
              ]),
            ),
          ]),
        ),
      ),
    );
  }
}
