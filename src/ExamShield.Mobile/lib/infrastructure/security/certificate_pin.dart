import 'dart:convert';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';

/// Immutable value object representing an SHA-256 certificate fingerprint.
///
/// Used to verify that a server certificate's DER bytes match a known, trusted value.
/// Fingerprints are stored as Base64-encoded SHA-256 digests (32 raw bytes → 44 Base64 chars).
class CertificatePin {
  final String _sha256Base64;

  const CertificatePin._(this._sha256Base64);

  /// Creates a [CertificatePin] from a Base64-encoded SHA-256 fingerprint.
  ///
  /// Throws [FormatException] if [base64Fingerprint] is not valid Base64.
  /// Throws [ArgumentError] if the decoded bytes are not exactly 32 (SHA-256 output size).
  factory CertificatePin.fromSha256Base64(String base64Fingerprint) {
    if (base64Fingerprint.isEmpty) {
      throw ArgumentError.value(base64Fingerprint, 'base64Fingerprint', 'Must not be empty.');
    }
    final decoded = base64.decode(base64Fingerprint); // throws FormatException if invalid
    if (decoded.length != 32) {
      throw ArgumentError.value(
        base64Fingerprint,
        'base64Fingerprint',
        'SHA-256 fingerprint must decode to exactly 32 bytes; got ${decoded.length}.',
      );
    }
    return CertificatePin._(base64Fingerprint);
  }

  /// Returns `true` when the SHA-256 of [derBytes] matches this pin.
  bool matches(Uint8List derBytes) {
    final digest = sha256.convert(derBytes);
    return base64.encode(digest.bytes) == _sha256Base64;
  }

  @override
  String toString() => 'sha256/$_sha256Base64';

  @override
  bool operator ==(Object other) =>
      other is CertificatePin && other._sha256Base64 == _sha256Base64;

  @override
  int get hashCode => _sha256Base64.hashCode;
}
