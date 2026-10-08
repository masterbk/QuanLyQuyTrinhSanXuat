import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

/// Màn ký tay bằng ngón tay khi Hoàn thành phiếu. Trả ảnh PNG (nền trong suốt) qua Navigator.pop, null nếu huỷ.
class ManKyTen extends StatefulWidget {
  final String tenNguoiKy;
  const ManKyTen({super.key, required this.tenNguoiKy});

  @override
  State<ManKyTen> createState() => _ManKyTenState();
}

class _ManKyTenState extends State<ManKyTen> {
  final List<List<Offset>> _net = [];
  Size _khung = Size.zero;

  bool get _daKy => _net.any((n) => n.length > 1);

  /// Vẽ lại các nét vào ảnh PNG đúng kích thước khung (x2 cho nét mịn khi in).
  Future<Uint8List?> _xuatAnh() async {
    const tyLe = 2.0;
    final rec = ui.PictureRecorder();
    final c = Canvas(rec);
    c.scale(tyLe);
    VeChuKy(_net).paint(c, _khung);
    final anh = await rec.endRecording().toImage((_khung.width * tyLe).round(), (_khung.height * tyLe).round());
    final b = await anh.toByteData(format: ui.ImageByteFormat.png);
    return b?.buffer.asUint8List();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Ký xác nhận hoàn thành')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          const Text('Ký vào khung dưới bằng ngón tay. Sau khi ký, phiếu được chốt và chỉ xem lại được.'),
          const SizedBox(height: 12),
          Expanded(
            child: LayoutBuilder(builder: (context, rang) {
              _khung = Size(rang.maxWidth, rang.maxHeight);
              return Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  border: Border.all(color: Colors.grey),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: GestureDetector(
                  key: const ValueKey('khung-ky'),
                  onPanStart: (d) => setState(() => _net.add([d.localPosition])),
                  onPanUpdate: (d) => setState(() => _net.last.add(d.localPosition)),
                  child: CustomPaint(painter: VeChuKy(_net), size: Size.infinite),
                ),
              );
            }),
          ),
          const SizedBox(height: 8),
          Text('Người ký: ${widget.tenNguoiKy}', style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 12),
          Row(children: [
            OutlinedButton.icon(
              onPressed: _net.isEmpty ? null : () => setState(_net.clear),
              icon: const Icon(Icons.refresh),
              label: const Text('Ký lại'),
            ),
            const Spacer(),
            FilledButton.icon(
              onPressed: () async {
                if (!_daKy) {
                  ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Vui lòng ký vào khung trước khi hoàn thành.')));
                  return;
                }
                final anh = await _xuatAnh();
                if (context.mounted) Navigator.pop(context, anh);
              },
              icon: const Icon(Icons.task_alt),
              label: const Text('Ký & Hoàn thành'),
            ),
          ]),
        ]),
      ),
    );
  }
}

/// Vẽ các nét chữ ký (dùng cho cả khung ký lẫn xuất ảnh).
class VeChuKy extends CustomPainter {
  final List<List<Offset>> net;
  VeChuKy(this.net);

  @override
  void paint(Canvas canvas, Size size) {
    final but = Paint()
      ..color = const Color(0xFF1A237E)
      ..strokeWidth = 2.5
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;
    for (final n in net) {
      if (n.length < 2) continue;
      final p = Path()..moveTo(n.first.dx, n.first.dy);
      for (final d in n.skip(1)) {
        p.lineTo(d.dx, d.dy);
      }
      canvas.drawPath(p, but);
    }
  }

  @override
  bool shouldRepaint(covariant VeChuKy old) => true;
}
