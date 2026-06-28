import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';
import 'package:examshield_mobile/domain/services/capture_service.dart';
import 'package:examshield_mobile/ui/screens/capture_screen.dart';

class MockCaptureService extends Mock implements CaptureService {}

Widget buildSubject(CaptureService service) => MaterialApp(
      home: Provider<CaptureService>.value(
        value: service,
        child: const CaptureScreen(),
      ),
    );

void main() {
  late MockCaptureService mockService;

  setUp(() => mockService = MockCaptureService());

  // NOTE: The camera plugin calls availableCameras() in initState, which
  // is a platform channel. In the test environment no camera is available,
  // so the screen renders its "Initializing camera…" fallback. We test all
  // UI elements that are visible before or without a camera.

  testWidgets('shows Capture Answer Sheet app bar title', (tester) async {
    await tester.pumpWidget(buildSubject(mockService));
    await tester.pump();
    expect(find.text('Capture Answer Sheet'), findsOneWidget);
  });

  testWidgets('shows Initializing camera text when no camera available',
      (tester) async {
    await tester.pumpWidget(buildSubject(mockService));
    await tester.pump();
    expect(find.text('Initializing camera…'), findsOneWidget);
  });

  testWidgets('shows Capture button', (tester) async {
    await tester.pumpWidget(buildSubject(mockService));
    await tester.pump();
    expect(find.widgetWithText(ElevatedButton, 'Capture'), findsOneWidget);
  });

  testWidgets('Capture button is enabled when not capturing', (tester) async {
    await tester.pumpWidget(buildSubject(mockService));
    await tester.pump();
    final btn = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'Capture'),
    );
    expect(btn.onPressed, isNotNull);
  });

  testWidgets('status area is hidden before any capture attempt', (tester) async {
    await tester.pumpWidget(buildSubject(mockService));
    await tester.pump();
    // Status container only appears when _status != null
    expect(find.text('Capturing…'), findsNothing);
    expect(find.text('Uploading…'), findsNothing);
  });

  testWidgets('shows scaffold with dark background', (tester) async {
    await tester.pumpWidget(buildSubject(mockService));
    await tester.pump();
    final scaffold = tester.widget<Scaffold>(find.byType(Scaffold));
    expect(scaffold.backgroundColor, const Color(0xFF0D1117));
  });
}
