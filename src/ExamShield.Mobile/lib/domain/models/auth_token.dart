class AuthToken {
  final String accessToken;
  final String role;
  final bool requiresMfa;
  final bool mfaSetupRequired;

  const AuthToken({
    required this.accessToken,
    required this.role,
    this.requiresMfa = false,
    this.mfaSetupRequired = false,
  });

  factory AuthToken.fromJson(Map<String, dynamic> json) => AuthToken(
        accessToken: json['token'] as String? ?? '',
        role: json['role'] as String? ?? '',
        requiresMfa: json['requiresMfa'] as bool? ?? false,
        mfaSetupRequired: json['mfaSetupRequired'] as bool? ?? false,
      );

  Map<String, dynamic> toJson() => {
        'token': accessToken,
        'role': role,
        'requiresMfa': requiresMfa,
        'mfaSetupRequired': mfaSetupRequired,
      };
}
