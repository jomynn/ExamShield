import 'package:flutter/material.dart';

/// Displays in-app notifications for the signed-in invigilator:
/// upload confirmations, verification results, sync errors, and server alerts.
class NotificationsScreen extends StatelessWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0D1117),
      appBar: AppBar(
        backgroundColor: const Color(0xFF161B22),
        title: const Text('Notifications', style: TextStyle(color: Colors.white)),
        automaticallyImplyLeading: false,
        actions: [
          TextButton(
            onPressed: () {},
            child: const Text('Clear all',
                style: TextStyle(color: Color(0xFF8B949E), fontSize: 12)),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: 8),
        children: const [
          _NotificationTile(
            icon: Icons.check_circle_outline,
            iconColor: Color(0xFF00FF88),
            title: 'Upload Verified',
            body: 'Answer sheet CAP-001 was verified successfully.',
            time: 'Just now',
          ),
          _NotificationTile(
            icon: Icons.warning_amber_outlined,
            iconColor: Color(0xFFFFAA00),
            title: 'Low Confidence OCR',
            body: 'Sheet CAP-002 routed to manual review (confidence 58%).',
            time: '5 min ago',
          ),
          _NotificationTile(
            icon: Icons.sync_problem_outlined,
            iconColor: Color(0xFFFF6B6B),
            title: 'Sync Failed',
            body: '3 captures pending upload — network unavailable.',
            time: '12 min ago',
          ),
        ],
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  final IconData icon;
  final Color iconColor;
  final String title;
  final String body;
  final String time;

  const _NotificationTile({
    required this.icon,
    required this.iconColor,
    required this.title,
    required this.body,
    required this.time,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        color: const Color(0xFF161B22),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFF30363D)),
      ),
      child: ListTile(
        leading: Icon(icon, color: iconColor, size: 28),
        title: Text(title,
            style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 2),
            Text(body, style: const TextStyle(color: Color(0xFF8B949E), fontSize: 13)),
            const SizedBox(height: 4),
            Text(time, style: const TextStyle(color: Color(0xFF555E6A), fontSize: 11)),
          ],
        ),
        isThreeLine: true,
      ),
    );
  }
}
