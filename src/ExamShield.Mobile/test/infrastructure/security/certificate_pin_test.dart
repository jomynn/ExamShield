import 'dart:convert';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../lib/infrastructure/security/certificate_pin.dart';

void main() {
  // Synthetic DER bytes used throughout these tests.
  final fakeDer = Uint8List.fromList(List.generate(64, (i) => i));
  final correctFp = base64.encode(sha256.convert(fakeDer).bytes);
  final wrongDer = Uint8List.fromList(List.generate(64, (i) => 255 - i));

  group('CertificatePin.fromSha256Base64', () {
    test('accepts a valid 32-byte SHA-256 base64 fingerprint', () {
      expect(() => CertificatePin.fromSha256Base64(correctFp), returnsNormally);
    });

    test('throws ArgumentError when decoded bytes are not 32 bytes', () {
      final short = base64.encode([1, 2, 3]); // 3 bytes
      expect(
        () => CertificatePin.fromSha256Base64(short),
        throwsA(isA<ArgumentError>()),
      );
    });

    test('throws FormatException when base64 string is invalid', () {
      expect(
        () => CertificatePin.fromSha256Base64('not!!valid@@base64'),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws ArgumentError for empty string', () {
      expect(
        () => CertificatePin.fromSha256Base64(''),
        throwsA(isA<ArgumentError>()),
      );
    });
  });

  group('CertificatePin.matches', () {
    test('returns true when DER bytes hash matches the pinned fingerprint', () {
      final pin = CertificatePin.fromSha256Base64(correctFp);
      expect(pin.matches(fakeDer), isTrue);
    });

    test('returns false when DER bytes do not match the pin', () {
      final pin = CertificatePin.fromSha256Base64(correctFp);
      expect(pin.matches(wrongDer), isFalse);
    });

    test('is consistent with crypto package SHA-256', () {
      final pin = CertificatePin.fromSha256Base64(correctFp);
      final digest = sha256.convert(fakeDer);
      final expected = base64.encode(digest.bytes);
      expect(expected, correctFp); // sanity-check the test setup
      expect(pin.matches(fakeDer), isTrue);
    });
  });

  group('CertificatePin equality', () {
    test('two pins with the same fingerprint are equal', () {
      final a = CertificatePin.fromSha256Base64(correctFp);
      final b = CertificatePin.fromSha256Base64(correctFp);
      expect(a, equals(b));
      expect(a.hashCode, equals(b.hashCode));
    });

    test('pins with different fingerprints are not equal', () {
      final a = CertificatePin.fromSha256Base64(correctFp);
      final altFp = base64.encode(sha256.convert(wrongDer).bytes);
      final b = CertificatePin.fromSha256Base64(altFp);
      expect(a, isNot(equals(b)));
    });
  });

  group('CertificatePin.toString', () {
    test('includes sha256 prefix and the fingerprint', () {
      final pin = CertificatePin.fromSha256Base64(correctFp);
      expect(pin.toString(), startsWith('sha256/'));
      expect(pin.toString(), contains(correctFp));
    });
  });
}
