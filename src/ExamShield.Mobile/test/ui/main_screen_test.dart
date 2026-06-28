import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';
import 'package:examshield_mobile/domain/services/capture_service.dart';
import 'package:examshield_mobile/domain/services/sync_service.dart';
import 'package:examshield_mobile/infrastructure/queue/offline_queue.dart';
import 'package:examshield_mobile/ui/screens/main_screen.dart';

class MockCaptureService extends Mock implements CaptureService {}
class MockSyncService extends Mock implements SyncService {}
class MockOfflineQueue extends Mock implements OfflineQueue {}

Widget buildSubject({
  required CaptureService captureService,
  required SyncService syncService,
}) =>
    MaterialApp(
      home: MultiProvider(
        providers: [
          Provider<CaptureService>.value(value: captureService),
          Provider<SyncService>.value(value: syncService),
        ],
        child: const MainScreen(),
      ),
    );

void main() {
  late MockCaptureService mockCapture;
  late MockSyncService mockSync;
  late MockOfflineQueue mockQueue;

  setUp(() {
    mockCapture = MockCaptureService();
    mockSync = MockSyncService();
    mockQueue = MockOfflineQueue();

    when(() => mockQueue.pendingCount()).thenAnswer((_) async => 0);
    when(() => mockCapture.offlineQueue).thenReturn(mockQueue);
    // watchAndSync returns an empty stream — no connectivity events in tests
    when(() => mockSync.watchAndSync()).thenAnswer((_) => const Stream.empty());
  });

  testWidgets('renders bottom navigation bar with 5 tabs', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    expect(find.byType(BottomNavigationBar), findsOneWidget);
    expect(find.byType(BottomNavigationBarItem), findsNWidgets(5));
  });

  testWidgets('shows Capture tab label', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    expect(find.text('Capture'), findsAtLeastNWidgets(1));
  });

  testWidgets('shows History tab label', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    expect(find.text('History'), findsOneWidget);
  });

  testWidgets('shows Sync tab label', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    expect(find.text('Sync'), findsOneWidget);
  });

  testWidgets('shows Notifications tab label', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    expect(find.text('Notifications'), findsOneWidget);
  });

  testWidgets('shows Profile tab label', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    expect(find.text('Profile'), findsOneWidget);
  });

  testWidgets('starts on Capture tab (index 0)', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    final nav = tester.widget<BottomNavigationBar>(
        find.byType(BottomNavigationBar));
    expect(nav.currentIndex, 0);
  });

  testWidgets('shows pending badge on Sync tab when items queued', (tester) async {
    when(() => mockQueue.pendingCount()).thenAnswer((_) async => 4);
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pumpAndSettle();
    expect(find.text('4'), findsOneWidget);
  });

  testWidgets('no badge shown on Sync tab when queue is empty', (tester) async {
    when(() => mockQueue.pendingCount()).thenAnswer((_) async => 0);
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pumpAndSettle();
    expect(find.byType(Badge), findsNothing);
  });

  testWidgets('calls watchAndSync on init', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    verify(() => mockSync.watchAndSync()).called(1);
  });

  testWidgets('tapping History tab switches screen', (tester) async {
    await tester.pumpWidget(buildSubject(
        captureService: mockCapture, syncService: mockSync));
    await tester.pump();
    // Stub myCaptures for HistoryScreen which loads on tap
    when(() => mockCapture.myCaptures()).thenAnswer((_) async => []);
    await tester.tap(find.text('History'));
    await tester.pumpAndSettle();
    final nav = tester.widget<BottomNavigationBar>(
        find.byType(BottomNavigationBar));
    expect(nav.currentIndex, 1);
  });
}
