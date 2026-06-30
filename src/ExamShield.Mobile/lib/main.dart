import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'domain/services/auth_service.dart';
import 'domain/services/capture_service.dart';
import 'domain/services/crypto_service.dart';
import 'domain/services/device_registration_service.dart';
import 'domain/services/sync_service.dart';
import 'infrastructure/api/api_client.dart';
import 'infrastructure/queue/sqlite_offline_queue.dart';
import 'infrastructure/security/network_security.dart';
import 'infrastructure/storage/secure_storage.dart';
import 'ui/screens/login_screen.dart';
import 'ui/screens/main_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  const baseUrl = String.fromEnvironment('API_URL', defaultValue: 'http://10.0.2.2:5083');
  final httpClient = await NetworkSecurity.createHttpClient();
  final api = ApiClient(baseUrl: baseUrl, httpClient: httpClient);
  const secureStorage = SecureStorage();
  final authService = AuthService(api: api, storage: secureStorage);
  final crypto = CryptoService();
  final offlineQueue = SqliteOfflineQueue();
  final captureService = CaptureService(
      api: api, storage: secureStorage, crypto: crypto, offlineQueue: offlineQueue);
  final deviceRegService = DeviceRegistrationService(
      crypto: crypto, api: api, storage: secureStorage);
  final syncService = SyncService(queue: offlineQueue, api: api, storage: secureStorage);

  runApp(
    MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: api),
        ChangeNotifierProvider(create: (_) => AuthNotifier(authService)),
        Provider<CaptureService>.value(value: captureService),
        Provider<DeviceRegistrationService>.value(value: deviceRegService),
        Provider<SyncService>.value(value: syncService),
      ],
      child: const ExamShieldApp(),
    ),
  );
}

class ExamShieldApp extends StatelessWidget {
  const ExamShieldApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ExamShield',
      debugShowCheckedModeBanner: false,
      theme: ThemeData.dark().copyWith(
        colorScheme: const ColorScheme.dark(primary: Color(0xFF00BFFF)),
        useMaterial3: true,
      ),
      home: Consumer<AuthNotifier>(
        builder: (_, auth, __) =>
            auth.isLoggedIn ? const MainScreen() : const LoginScreen(),
      ),
    );
  }
}
