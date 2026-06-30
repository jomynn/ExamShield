import 'dart:io';
import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:http/io_client.dart';

/// Creates [http.Client] instances with optional certificate pinning.
///
/// Production: call [createPinned] with the DER/PEM bytes of the API's CA or leaf certificate.
/// The resulting client will only trust that specific certificate — all others are rejected,
/// preventing MitM attacks even from publicly-trusted but rogue certificate authorities.
///
/// Development / test: call [createDevelopment], which trusts the system CA store (no pinning).
class PinnedHttpClientFactory {
  PinnedHttpClientFactory._();

  /// Returns an [http.Client] that only trusts [trustedCertBytes].
  ///
  /// System root CAs are excluded (`withTrustedRoots: false`), so the server
  /// must present a certificate that chains to [trustedCertBytes].
  ///
  /// Throws [TlsException] if [trustedCertBytes] are not valid PEM or DER certificate data.
  static http.Client createPinned({required Uint8List trustedCertBytes}) {
    final context = SecurityContext(withTrustedRoots: false)
      ..setTrustedCertificatesBytes(trustedCertBytes);

    final ioClient = HttpClient(context: context)
      ..badCertificateCallback = (_, __, ___) => false; // Never override; reject anything untrusted

    return IOClient(ioClient);
  }

  /// Returns a standard [http.Client] that trusts the platform's system CA store.
  ///
  /// Use only in development and automated testing — never in production builds.
  static http.Client createDevelopment() => http.Client();
}
