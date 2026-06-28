import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../domain/services/capture_service.dart';
import '../../domain/services/sync_service.dart';
import 'capture_screen.dart';
import 'history_screen.dart';
import 'notifications_screen.dart';
import 'sync_screen.dart';

class MainScreen extends StatefulWidget {
  const MainScreen({super.key});

  @override
  State<MainScreen> createState() => _MainScreenState();
}

class _MainScreenState extends State<MainScreen> {
  int _tab = 0;
  int _pendingCount = 0;

  static const _screens = [
    CaptureScreen(),
    HistoryScreen(),
    SyncScreen(),
    NotificationsScreen(),
  ];

  @override
  void initState() {
    super.initState();
    _refreshPendingCount();
    _startSyncWatcher();
  }

  Future<void> _refreshPendingCount() async {
    final count = await context.read<CaptureService>().offlineQueue.pendingCount();
    if (mounted) setState(() => _pendingCount = count);
  }

  void _startSyncWatcher() {
    context.read<SyncService>().watchAndSync().listen((_) => _refreshPendingCount());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0D1117),
      body: _screens[_tab],
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _tab,
        onTap: (i) { setState(() => _tab = i); _refreshPendingCount(); },
        backgroundColor: const Color(0xFF161B22),
        selectedItemColor: const Color(0xFF00BFFF),
        unselectedItemColor: const Color(0xFF8B949E),
        items: [
          const BottomNavigationBarItem(icon: Icon(Icons.camera_alt), label: 'Capture'),
          const BottomNavigationBarItem(icon: Icon(Icons.history), label: 'History'),
          BottomNavigationBarItem(
            label: 'Sync',
            icon: _pendingCount > 0
                ? Badge(
                    label: Text('$_pendingCount'),
                    child: const Icon(Icons.sync),
                  )
                : const Icon(Icons.sync),
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.notifications_outlined),
            label: 'Notifications',
          ),
        ],
      ),
    );
  }
}
