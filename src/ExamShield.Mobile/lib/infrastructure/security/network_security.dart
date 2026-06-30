import 'dart:typed_data';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;

import 'pinned_http_client_factory.dart';

/// Entry-point for creating the application's HTTP client.
///
/// In **release** builds: loads the pinned CA certificate from
/// `assets/certs/api_ca.pem` and creates a client that rejects any TLS
/// connection whose certificate does not chain to that CA.
///
/// In **debug / profile** builds: returns a development client that trusts
/// the platform CA store (needed to reach local dev servers over HTTP).
class NetworkSecurity {
  NetworkSecurity._();

  /// The Flutter asset path where the pinned API CA certificate must be placed.
  static const _certAssetPath = 'assets/certs/api_ca.pem';

  /// Creates and returns an appropriate [http.Client] for the current build mode.
  ///
  /// Must be called after [WidgetsFlutterBinding.ensureInitialized] because it
  /// may load assets from the bundle.
  static Future<http.Client> createHttpClient() async {
    if (!kReleaseMode) {
      // Debug / profile builds: trust the system CA store.
      return PinnedHttpClientFactory.createDevelopment();
    }

    final data = await rootBundle.load(_certAssetPath);
    final certBytes = data.buffer.asUint8List();
    return PinnedHttpClientFactory.createPinned(trustedCertBytes: certBytes);
  }
}
