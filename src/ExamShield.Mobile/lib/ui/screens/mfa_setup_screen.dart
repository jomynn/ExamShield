import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import '../../domain/services/auth_service.dart';
import '../../infrastructure/api/api_client.dart';

class MfaSetupScreen extends StatefulWidget {
  const MfaSetupScreen({super.key});

  @override
  State<MfaSetupScreen> createState() => _MfaSetupScreenState();
}

class _MfaSetupScreenState extends State<MfaSetupScreen> {
  final _codeCtrl = TextEditingController();
  final _form = GlobalKey<FormState>();

  MfaSetupInfo? _setupInfo;
  bool _loading = true;
  bool _confirming = false;
  bool _done = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _codeCtrl.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final info = await context.read<AuthNotifier>().beginMfaSetup();
    if (!mounted) return;
    setState(() {
      _setupInfo = info;
      _loading = false;
    });
  }

  Future<void> _confirm() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _confirming = true);
    try {
      await context.read<AuthNotifier>().confirmMfaSetup(code: _codeCtrl.text.trim());
      if (!mounted) return;
      final err = context.read<AuthNotifier>().error;
      if (err != null) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(err), backgroundColor: Colors.red));
      } else {
        setState(() => _done = true);
      }
    } finally {
      if (mounted) setState(() => _confirming = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0D1117),
      appBar: AppBar(
        backgroundColor: const Color(0xFF161B22),
        title: const Text('Set Up Two-Factor Authentication',
            style: TextStyle(color: Colors.white, fontSize: 16)),
        automaticallyImplyLeading: false,
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(32),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 400),
            child: _loading
                ? const CircularProgressIndicator(color: Color(0xFF00BFFF))
                : _done
                    ? _buildDone()
                    : _buildSetupForm(),
          ),
        ),
      ),
    );
  }

  Widget _buildSetupForm() {
    if (_setupInfo == null) {
      return const Text('Failed to load MFA setup. Please log in again.',
          style: TextStyle(color: Colors.red));
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.security, color: Color(0xFF00BFFF), size: 40),
        const SizedBox(height: 16),
        const Text('MFA Setup Required',
            style: TextStyle(
                color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        const Text(
          'Your role requires two-factor authentication. '
          'Scan the QR code below with an authenticator app '
          '(e.g. Google Authenticator or Authy), then enter the 6-digit code.',
          style: TextStyle(color: Color(0xFF8B949E), fontSize: 14),
        ),
        const SizedBox(height: 24),
        _buildSecretCard(),
        const SizedBox(height: 24),
        Form(
          key: _form,
          child: Column(
            children: [
              TextFormField(
                controller: _codeCtrl,
                keyboardType: TextInputType.number,
                maxLength: 6,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                style: const TextStyle(color: Colors.white, letterSpacing: 8),
                decoration: InputDecoration(
                  labelText: 'Verification Code',
                  labelStyle: const TextStyle(color: Color(0xFF8B949E)),
                  filled: true,
                  fillColor: const Color(0xFF161B22),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(8),
                    borderSide: const BorderSide(color: Color(0xFF30363D)),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(8),
                    borderSide: const BorderSide(color: Color(0xFF30363D)),
                  ),
                  counterStyle: const TextStyle(color: Color(0xFF8B949E)),
                ),
                validator: (v) => (v == null || v.length != 6)
                    ? 'Enter the 6-digit code from your authenticator app'
                    : null,
              ),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _confirming ? null : _confirm,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF00BFFF),
                    foregroundColor: Colors.black,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8)),
                  ),
                  child: _confirming
                      ? const SizedBox(
                          height: 18,
                          width: 18,
                          child: CircularProgressIndicator(
                              strokeWidth: 2, color: Colors.black),
                        )
                      : const Text('Verify & Enable MFA',
                          style: TextStyle(fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildSecretCard() {
    final secret = _setupInfo!.secret;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFF161B22),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFF30363D)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Manual Entry Key',
              style: TextStyle(color: Color(0xFF8B949E), fontSize: 12)),
          const SizedBox(height: 4),
          Row(
            children: [
              Expanded(
                child: Text(secret,
                    style: const TextStyle(
                        color: Color(0xFF00BFFF),
                        fontSize: 14,
                        fontFamily: 'monospace',
                        letterSpacing: 2)),
              ),
              IconButton(
                icon: const Icon(Icons.copy, color: Color(0xFF8B949E), size: 18),
                onPressed: () {
                  Clipboard.setData(ClipboardData(text: secret));
                  ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Secret copied to clipboard')));
                },
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildDone() {
    return Column(
      children: [
        const Icon(Icons.check_circle, color: Color(0xFF00FF88), size: 64),
        const SizedBox(height: 16),
        const Text('MFA Enabled',
            style: TextStyle(
                color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        const Text(
          'Two-factor authentication is now active on your account. '
          'You will need your authenticator app on every sign-in.',
          textAlign: TextAlign.center,
          style: TextStyle(color: Color(0xFF8B949E)),
        ),
        const SizedBox(height: 24),
        const CircularProgressIndicator(color: Color(0xFF00BFFF)),
        const SizedBox(height: 8),
        const Text('Signing you in…',
            style: TextStyle(color: Color(0xFF8B949E), fontSize: 12)),
      ],
    );
  }
}
