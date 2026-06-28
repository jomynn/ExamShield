import 'dart:convert';
import 'dart:typed_data';
import 'package:http/http.dart' as http;
import '../../domain/models/auth_token.dart';
import '../../domain/models/capture.dart';

class ApiException implements Exception {
  final int statusCode;
  final String message;
  const ApiException({required this.statusCode, required this.message});

  @override
  String toString() => 'ApiException($statusCode): $message';
}

class MfaSetupInfo {
  final String secret;
  final String qrUri;
  const MfaSetupInfo({required this.secret, required this.qrUri});
}

class ApiClient {
  final String baseUrl;
  final http.Client _http;

  ApiClient({required this.baseUrl, http.Client? httpClient})
      : _http = httpClient ?? http.Client();

  Future<AuthToken> login({required String email, required String password}) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'email': email, 'password': password}),
    );
    _assertOk(res);
    return AuthToken.fromJson(jsonDecode(res.body) as Map<String, dynamic>);
  }

  Future<MfaSetupInfo> setupMfa(String token) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/auth/mfa/setup'),
      headers: {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'},
    );
    _assertOk(res);
    final json = jsonDecode(res.body) as Map<String, dynamic>;
    return MfaSetupInfo(
      secret: json['secret'] as String,
      qrUri: json['qrUri'] as String,
    );
  }

  Future<void> verifyMfaSetup({required String code, required String token}) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/auth/mfa/verify'),
      headers: {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'},
      body: jsonEncode({'code': code}),
    );
    _assertOk(res);
  }

  Future<AuthToken> mfaLogin({
    required String email,
    required String password,
    required String code,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/auth/mfa/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'email': email, 'password': password, 'code': code}),
    );
    _assertOk(res);
    return AuthToken.fromJson(jsonDecode(res.body) as Map<String, dynamic>);
  }

  Future<String> registerDevice({
    required String name,
    required String publicKeyBase64,
    required String token,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/devices/register'),
      headers: {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'},
      body: jsonEncode({'name': name, 'publicKey': publicKeyBase64}),
    );
    _assertOk(res);
    final json = jsonDecode(res.body) as Map<String, dynamic>;
    return json['deviceId'] as String;
  }

  Future<String> registerCapture(CaptureRequest capture, String token) async {
    final body = jsonEncode({
      'examId': capture.examId,
      'studentId': capture.studentId,
      'deviceId': capture.deviceId,
      'pageNumber': capture.pageNumber,
      'hashHex': capture.hashHex,
      'signatureBytes': base64Encode(capture.signatureBytes),
    });
    final res = await _http.post(
      Uri.parse('$baseUrl/capture'),
      headers: {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'},
      body: body,
    );
    _assertOk(res);
    final json = jsonDecode(res.body) as Map<String, dynamic>;
    return json['captureId'] as String;
  }

  Future<void> uploadImage(String captureId, Uint8List imageBytes, String token) async {
    final req = http.MultipartRequest('POST', Uri.parse('$baseUrl/upload'));
    req.headers['Authorization'] = 'Bearer $token';
    req.fields['captureId'] = captureId;
    req.files.add(http.MultipartFile.fromBytes('file', imageBytes, filename: '$captureId.jpg'));
    final streamed = await req.send();
    if (streamed.statusCode >= 400) {
      throw ApiException(statusCode: streamed.statusCode, message: 'Upload failed');
    }
  }

  Future<List<CaptureRecord>> getMyCaptures(String token) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/captures'),
      headers: {'Authorization': 'Bearer $token'},
    );
    _assertOk(res);
    final list = (jsonDecode(res.body) as Map<String, dynamic>)['captures'] as List;
    return list.map((e) => CaptureRecord.fromJson(e as Map<String, dynamic>)).toList();
  }

  void _assertOk(http.Response res) {
    if (res.statusCode >= 400) {
      String msg = res.body;
      try {
        final json = jsonDecode(res.body) as Map<String, dynamic>;
        msg = (json['detail'] ?? json['message'] ?? msg) as String;
      } catch (_) {}
      throw ApiException(statusCode: res.statusCode, message: msg);
    }
  }
}
