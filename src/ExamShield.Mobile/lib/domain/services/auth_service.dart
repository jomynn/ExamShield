import 'package:flutter/foundation.dart';
import '../../infrastructure/api/api_client.dart';
import '../../infrastructure/storage/secure_storage.dart';
import '../models/auth_token.dart';

class AuthService {
  final ApiClient api;
  final SecureStorage storage;

  const AuthService({required this.api, required this.storage});

  Future<AuthToken> login({required String email, required String password}) async {
    final token = await api.login(email: email, password: password);
    if (!token.requiresMfa && !token.mfaSetupRequired) {
      await storage.saveToken(token);
    }
    return token;
  }

  Future<AuthToken> completeMfaLogin({
    required String email,
    required String password,
    required String code,
  }) async {
    final token = await api.mfaLogin(email: email, password: password, code: code);
    await storage.saveToken(token);
    return token;
  }

  Future<MfaSetupInfo> beginMfaSetup(String accessToken) =>
      api.setupMfa(accessToken);

  Future<void> confirmMfaSetup({required String code, required String accessToken}) =>
      api.verifyMfaSetup(code: code, token: accessToken);

  Future<void> logout() => storage.clearToken();

  Future<bool> isLoggedIn() async => (await storage.loadToken()) != null;

  Future<AuthToken?> currentToken() => storage.loadToken();
}

class AuthNotifier extends ChangeNotifier {
  final AuthService _auth;
  bool _isLoggedIn = false;
  bool _requiresMfa = false;
  bool _mfaSetupRequired = false;
  String? _role;
  String? _error;
  String? _pendingEmail;
  String? _pendingPassword;
  String? _pendingSetupToken;

  bool get isLoggedIn => _isLoggedIn;
  bool get requiresMfa => _requiresMfa;
  bool get mfaSetupRequired => _mfaSetupRequired;
  String? get role => _role;
  String? get error => _error;

  AuthNotifier(this._auth);

  Future<void> login({required String email, required String password}) async {
    _error = null;
    try {
      final token = await _auth.login(email: email, password: password);
      if (token.mfaSetupRequired) {
        _mfaSetupRequired = true;
        _requiresMfa = false;
        _pendingEmail = email;
        _pendingPassword = password;
        // The server returns no access token until MFA is enrolled; store what we have.
        _pendingSetupToken = token.accessToken.isEmpty ? null : token.accessToken;
      } else if (token.requiresMfa) {
        _requiresMfa = true;
        _mfaSetupRequired = false;
        _pendingEmail = email;
        _pendingPassword = password;
      } else {
        _role = token.role;
        _isLoggedIn = true;
        _requiresMfa = false;
        _mfaSetupRequired = false;
        _pendingEmail = null;
        _pendingPassword = null;
      }
    } on ApiException catch (e) {
      _error = e.message;
    } finally {
      notifyListeners();
    }
  }

  Future<void> completeMfaLogin({required String code}) async {
    _error = null;
    try {
      final token = await _auth.completeMfaLogin(
        email: _pendingEmail!,
        password: _pendingPassword!,
        code: code,
      );
      _role = token.role;
      _isLoggedIn = true;
      _requiresMfa = false;
      _mfaSetupRequired = false;
      _pendingEmail = null;
      _pendingPassword = null;
    } on ApiException catch (e) {
      _error = e.message;
    } finally {
      notifyListeners();
    }
  }

  /// Returns `MfaSetupInfo` (secret + QR URI) to display the enrollment screen.
  /// Callers must call [confirmMfaSetup] after the user scans the QR and enters a code.
  Future<MfaSetupInfo?> beginMfaSetup() async {
    _error = null;
    try {
      final setupToken = _pendingSetupToken ?? '';
      return await _auth.beginMfaSetup(setupToken);
    } on ApiException catch (e) {
      _error = e.message;
      notifyListeners();
      return null;
    }
  }

  /// Confirms the TOTP code entered by the user during MFA enrollment.
  /// On success, re-logs in (enforcement now satisfied) and transitions to [isLoggedIn].
  Future<void> confirmMfaSetup({required String code}) async {
    _error = null;
    try {
      await _auth.confirmMfaSetup(
        code: code,
        accessToken: _pendingSetupToken ?? '',
      );
      // Enrollment done — perform the full login again so we get a real access token.
      final email = _pendingEmail!;
      final password = _pendingPassword!;
      _mfaSetupRequired = false;
      _pendingSetupToken = null;
      await login(email: email, password: password);
    } on ApiException catch (e) {
      _error = e.message;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    await _auth.logout();
    _isLoggedIn = false;
    _requiresMfa = false;
    _mfaSetupRequired = false;
    _role = null;
    _pendingEmail = null;
    _pendingPassword = null;
    _pendingSetupToken = null;
    notifyListeners();
  }

  Future<String?> currentToken() async {
    final token = await _auth.currentToken();
    return token?.accessToken;
  }
}
