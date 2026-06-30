import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import '../../../lib/infrastructure/security/pinned_http_client_factory.dart';

// Minimal self-signed CA cert for testing (CN=ExamShield-Test-CA, RSA 2048, valid 10 years).
// Generated once; used only to verify the factory accepts valid PEM bytes.
const _testCaPem = '''
-----BEGIN CERTIFICATE-----
MIIDGzCCAgOgAwIBAgIUczp73KMqHBNKQs8mdZCWmiLZKqwwDQYJKoZIhvcNAQEL
BQAwHTEbMBkGA1UEAwwSRXhhbVNoaWVsZC1UZXN0LUNBMB4XDTI2MDYzMDE1NTUz
MVoXDTM2MDYyNzE1NTUzMVowHTEbMBkGA1UEAwwSRXhhbVNoaWVsZC1UZXN0LUNB
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAlsRU8fjP7u9DP2X92E9K
dqk1BSvLfrZGlcSGs7PQnxIujWdOWbYmZNcTLeKHrkyZplyJmIjYMY7QoCL5StoB
DqE3YZ/BY2KzNVxFDdY9VwbPdhSh3t7bE5lJELmLE+9a9gzZ0pr6otBjzMOYuZIE
6dMTqza4MvsVd3FVoL1LQDQ6ApaBZ7jZJKVX7bY3+elmp0qNia8CMP5nJ+QvmqJ0
f0u9Si7/bH8QFF4xzUjT61QldEpybjIj1N59lRBAR6TrHSvOD3X4yz89Qv8gJpwW
+fGqa8AeKhPcDtk2L+G76qw+viUcmE/6VwMhj0sB21NxX4+llVCNj4Jzox4Chdr2
8QIDAQABo1MwUTAdBgNVHQ4EFgQUGCzr3UvaegxmQ1gYxNsDUneIGXwwHwYDVR0j
BBgwFoAUGCzr3UvaegxmQ1gYxNsDUneIGXwwDwYDVR0TAQH/BAUwAwEB/zANBgkq
hkiG9w0BAQsFAAOCAQEABwD9laSND0CL3OJUt0gxD+BmlbUafoI+XC09FeBhMVtA
v7Zif+XFFjXyR8sUSLct3UepD9B0EYbyH14tqEGVWVcwiJOLEqhno/JaX6pqP2pY
MtbWryXYWKHYgC7zIIgkfpbthdPlKsBrZt8wc2GNUIq5K5UnmJ8XZcSbau0knWBc
OCz3n9+quU6C5WhfNxO66oVVuIjfcSfeS218jF1Pw0RjOqzBqWLBXDE4I986YlQY
zEBCOLArlsmcDtix574751OxV717vFKXSRm4HQua3SSn2h4tAlOUPpuaXEe9q3jx
oPCPMAhp3E+GTrjnXnCuOeo2HVHfAH7KIGveJzl3ng==
-----END CERTIFICATE-----
''';

void main() {
  group('PinnedHttpClientFactory.createDevelopment', () {
    test('returns a non-null http.Client', () {
      final client = PinnedHttpClientFactory.createDevelopment();
      expect(client, isA<http.Client>());
      client.close();
    });
  });

  group('PinnedHttpClientFactory.createPinned', () {
    test('returns a non-null http.Client when given a valid PEM cert', () {
      final bytes = Uint8List.fromList(_testCaPem.codeUnits);
      final client = PinnedHttpClientFactory.createPinned(trustedCertBytes: bytes);
      expect(client, isA<http.Client>());
      client.close();
    });

    test('throws TlsException when cert bytes are invalid PEM', () {
      expect(
        () => PinnedHttpClientFactory.createPinned(
          trustedCertBytes: Uint8List.fromList('not a cert'.codeUnits),
        ),
        throwsA(isA<TlsException>()),
      );
    });

    test('throws TlsException when cert bytes are empty', () {
      expect(
        () => PinnedHttpClientFactory.createPinned(
          trustedCertBytes: Uint8List(0),
        ),
        throwsA(isA<TlsException>()),
      );
    });
  });
}
