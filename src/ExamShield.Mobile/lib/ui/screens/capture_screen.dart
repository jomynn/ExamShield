import 'dart:typed_data';
import 'package:camera/camera.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image/image.dart' as img;
import 'package:provider/provider.dart';
import '../../domain/services/capture_service.dart';

class CaptureScreen extends StatefulWidget {
  const CaptureScreen({super.key});

  @override
  State<CaptureScreen> createState() => _CaptureScreenState();
}

class _CaptureScreenState extends State<CaptureScreen> {
  CameraController? _controller;
  bool _capturing = false;
  String? _status;

  // Preview step: show enhanced image before uploading
  Uint8List? _previewBytes;

  @override
  void initState() {
    super.initState();
    _initCamera();
  }

  Future<void> _initCamera() async {
    final cameras = await availableCameras();
    if (cameras.isEmpty) return;
    _controller = CameraController(
      cameras.first,
      ResolutionPreset.veryHigh,
      enableAudio: false,
      imageFormatGroup: ImageFormatGroup.jpeg,
    );
    await _controller!.initialize();
    // Lock auto-exposure and auto-focus for consistent scans
    await _controller!.setExposureMode(ExposureMode.locked);
    await _controller!.setFocusMode(FocusMode.auto);
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  // -------------------------------------------------------------------------
  // Step 1 — Capture + enhance
  // -------------------------------------------------------------------------

  Future<void> _captureAndEnhance() async {
    if (_controller == null || !_controller!.value.isInitialized || _capturing) return;
    setState(() { _capturing = true; _status = 'Capturing…'; });

    try {
      final file = await _controller!.takePicture();
      final raw = await file.readAsBytes();
      setState(() => _status = 'Enhancing image…');

      // Run in isolate to keep UI responsive
      final enhanced = await compute(_enhanceDocumentIsolate, raw);
      setState(() {
        _previewBytes = enhanced;
        _capturing = false;
        _status = null;
      });
    } catch (e) {
      setState(() { _status = 'Error: $e'; _capturing = false; });
    }
  }

  // -------------------------------------------------------------------------
  // Step 2 — Hash, sign, upload
  // -------------------------------------------------------------------------

  Future<void> _upload(Uint8List bytes) async {
    setState(() { _capturing = true; _status = 'Hashing & signing…'; });
    try {
      final service = context.read<CaptureService>();
      final captureId = await service.hashSignAndRegister(bytes);
      setState(() => _status = 'Uploading…');
      await service.uploadWithFallback(captureId, bytes);
      final pending = await service.offlineQueue.pendingCount();
      setState(() {
        _previewBytes = null;
        _status = pending > 0
            ? 'Queued offline — $pending pending upload(s)'
            : 'Done — capture ID: $captureId';
      });
    } catch (e) {
      setState(() => _status = 'Upload error: $e');
    } finally {
      setState(() => _capturing = false);
    }
  }

  void _retake() => setState(() { _previewBytes = null; _status = null; });

  // -------------------------------------------------------------------------
  // Build
  // -------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0D1117),
      appBar: AppBar(
        title: const Text('Capture Answer Sheet'),
        backgroundColor: const Color(0xFF161B22),
        foregroundColor: Colors.white,
      ),
      body: _previewBytes != null
          ? _PreviewStep(
              imageBytes: _previewBytes!,
              status: _status,
              busy: _capturing,
              onUpload: () => _upload(_previewBytes!),
              onRetake: _retake,
            )
          : _CameraStep(
              controller: _controller,
              status: _status,
              capturing: _capturing,
              onCapture: _captureAndEnhance,
            ),
    );
  }
}

// ---------------------------------------------------------------------------
// Camera step with document-alignment guide overlay
// ---------------------------------------------------------------------------

class _CameraStep extends StatelessWidget {
  const _CameraStep({
    required this.controller,
    required this.status,
    required this.capturing,
    required this.onCapture,
  });

  final CameraController? controller;
  final String? status;
  final bool capturing;
  final VoidCallback onCapture;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Expanded(
          child: Stack(
            fit: StackFit.expand,
            children: [
              if (controller?.value.isInitialized == true)
                CameraPreview(controller!)
              else
                const Center(
                  child: Text('Initializing camera…',
                      style: TextStyle(color: Colors.white54)),
                ),
              // Document alignment guide
              CustomPaint(painter: _DocumentGuidePainter()),
            ],
          ),
        ),
        if (status != null)
          _StatusBar(message: status!),
        Padding(
          padding: const EdgeInsets.all(24),
          child: ElevatedButton.icon(
            onPressed: capturing ? null : onCapture,
            icon: capturing
                ? const SizedBox(
                    width: 18, height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black),
                  )
                : const Icon(Icons.document_scanner),
            label: Text(capturing ? 'Processing…' : 'Scan Document'),
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF00BFFF),
              foregroundColor: Colors.black,
              minimumSize: const Size(double.infinity, 52),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
          ),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Preview/confirm step
// ---------------------------------------------------------------------------

class _PreviewStep extends StatelessWidget {
  const _PreviewStep({
    required this.imageBytes,
    required this.status,
    required this.busy,
    required this.onUpload,
    required this.onRetake,
  });

  final Uint8List imageBytes;
  final String? status;
  final bool busy;
  final VoidCallback onUpload;
  final VoidCallback onRetake;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Expanded(
          child: InteractiveViewer(
            child: Image.memory(imageBytes, fit: BoxFit.contain),
          ),
        ),
        if (status != null)
          _StatusBar(message: status!),
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
          child: Column(
            children: [
              ElevatedButton.icon(
                onPressed: busy ? null : onUpload,
                icon: busy
                    ? const SizedBox(
                        width: 18, height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black),
                      )
                    : const Icon(Icons.cloud_upload),
                label: Text(busy ? 'Uploading…' : 'Upload & Verify'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF00BFFF),
                  foregroundColor: Colors.black,
                  minimumSize: const Size(double.infinity, 52),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                ),
              ),
              const SizedBox(height: 12),
              TextButton.icon(
                onPressed: busy ? null : onRetake,
                icon: const Icon(Icons.refresh, color: Colors.white60),
                label: const Text('Retake', style: TextStyle(color: Colors.white60)),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Status bar
// ---------------------------------------------------------------------------

class _StatusBar extends StatelessWidget {
  const _StatusBar({required this.message});
  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      color: const Color(0xFF161B22),
      child: Text(
        message,
        style: const TextStyle(color: Color(0xFF00BFFF), fontSize: 13),
        textAlign: TextAlign.center,
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Document guide overlay painter
// ---------------------------------------------------------------------------

class _DocumentGuidePainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;

    // Semi-transparent dark mask outside the guide area
    const hPad = 24.0;
    final vPad = h * 0.12;
    final guideRect = Rect.fromLTRB(hPad, vPad, w - hPad, h - vPad);

    final maskPath = Path()
      ..addRect(Rect.fromLTWH(0, 0, w, h))
      ..addRect(guideRect)
      ..fillType = PathFillType.evenOdd;

    canvas.drawPath(maskPath, Paint()..color = Colors.black.withAlpha(120));

    // Guide border
    final borderPaint = Paint()
      ..color = const Color(0xFF00BFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.0;
    canvas.drawRect(guideRect, borderPaint);

    // Corner tick marks
    const tick = 24.0;
    final corners = <List<Offset>>[
      [guideRect.topLeft, guideRect.topLeft + const Offset(tick, 0), guideRect.topLeft + const Offset(0, tick)],
      [guideRect.topRight, guideRect.topRight - const Offset(tick, 0), guideRect.topRight + const Offset(0, tick)],
      [guideRect.bottomLeft, guideRect.bottomLeft + const Offset(tick, 0), guideRect.bottomLeft - const Offset(0, tick)],
      [guideRect.bottomRight, guideRect.bottomRight - const Offset(tick, 0), guideRect.bottomRight - const Offset(0, tick)],
    ];

    final tickPaint = Paint()
      ..color = const Color(0xFF00BFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3.5
      ..strokeCap = StrokeCap.round;

    for (final corner in corners) {
      canvas.drawLine(corner[0], corner[1], tickPaint);
      canvas.drawLine(corner[0], corner[2], tickPaint);
    }

    // Instruction label
    final tp = TextPainter(
      text: const TextSpan(
        text: 'Align answer sheet within frame',
        style: TextStyle(color: Colors.white70, fontSize: 12),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    tp.paint(canvas, Offset((w - tp.width) / 2, guideRect.top - 22));
  }

  @override
  bool shouldRepaint(_DocumentGuidePainter old) => false;
}

// ---------------------------------------------------------------------------
// Image enhancement — runs in a separate isolate via compute()
// Grayscale + contrast boost makes bubble detection more reliable.
// ---------------------------------------------------------------------------

Uint8List _enhanceDocumentIsolate(Uint8List rawJpeg) {
  final decoded = img.decodeImage(rawJpeg);
  if (decoded == null) return rawJpeg;

  // Grayscale reduces file size and eliminates color noise on bubble sheets
  var processed = img.grayscale(decoded);

  // Contrast boost: whites → 255, blacks → 0, contrast multiplier 1.4
  processed = img.adjustColor(processed, contrast: 1.4);

  return Uint8List.fromList(img.encodeJpg(processed, quality: 92));
}
