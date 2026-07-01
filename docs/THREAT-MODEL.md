# ExamShield — Threat Model

**Classification:** Internal — Security-Sensitive  
**Version:** 1.0 (2026-06-30)  
**Methodology:** STRIDE per component + attack tree summary

---

## 1. System Overview

ExamShield processes paper answer sheets at national examination scale. The security objectives are:

| Objective | Requirement |
|---|---|
| **Integrity** | No answer sheet may be modified after capture without cryptographic detection |
| **Non-repudiation** | Every capture is signed by a device key; every action is recorded in an append-only audit log |
| **Confidentiality** | Answer sheet images are encrypted at rest; access is gated by an allowlist policy |
| **Availability** | The system must handle concurrent uploads from thousands of invigilators |
| **Auditability** | Full chain of custody is verifiable by any authorized party at any time |

---

## 2. Assets

| Asset | Sensitivity | Where Stored |
|---|---|---|
| Answer sheet images (plaintext) | **Critical** | Memory only (in-flight); MinIO/S3 stores ciphertext only |
| Answer sheet images (ciphertext) | High | MinIO/S3 with Object Lock (COMPLIANCE mode) |
| Per-image DEK (plaintext) | **Critical** | Memory only during encrypt/decrypt; never persisted in plaintext |
| Per-image DEK (encrypted) | High | `Captures.EncryptedDek` column in PostgreSQL |
| Master encryption key | **Critical** | Config / HashiCorp Vault / AWS KMS / Azure Key Vault |
| Device private keys | **Critical** | Secure Enclave / Android Keystore on each device |
| Device public keys | Medium | `Devices.PublicKey` in PostgreSQL |
| JWT signing key | **Critical** | Config / secrets manager |
| Audit log entries | High | PostgreSQL (append-only, no DELETE/UPDATE path) |
| User credentials (hashed) | High | PostgreSQL (bcrypt) |
| MFA TOTP secrets | High | PostgreSQL (encrypted at rest) |
| Session / refresh tokens | High | PostgreSQL + HttpOnly cookie (dashboard); SecureStorage (mobile) |
| Answer keys | **Critical** | PostgreSQL — readable only by ScoringEngine role |
| OCR results | High | PostgreSQL |
| Scoring results / rankings | High | PostgreSQL |

---

## 3. Trust Boundaries

```
┌─────────────────────────────────────┐
│  Public Internet                    │
│  • Invigilators (mobile app)        │
│  • Students (portal)                │
│  • Anonymous verifiers (public QR)  │
└─────────────┬───────────────────────┘
              │  TLS 1.3 (mandatory)
┌─────────────▼───────────────────────┐
│  DMZ / Load Balancer                │
│  • Rate limiting (per-IP + per-JWT) │
│  • DDoS mitigation                  │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│  Application Cluster (Kubernetes)   │
│  • ExamShield API pods              │
│  • OCR Service pods                 │
└─────┬──────────────────────┬────────┘
      │                      │
┌─────▼──────┐        ┌──────▼──────┐
│  Data Tier  │        │  Message Bus│
│  PostgreSQL │        │  RabbitMQ   │
│  Redis      │        └─────────────┘
└─────┬───────┘
      │
┌─────▼───────────────────────────────┐
│  Object Storage                     │
│  MinIO (dev) / S3 Object Lock (prod)│
│  AES-256-GCM ciphertext only        │
└─────────────────────────────────────┘
```

---

## 4. Threat Actors

| Actor | Capability | Motivation |
|---|---|---|
| **Malicious invigilator** | Physical access to answer sheets; authenticated API access; may collude with student | Alter a specific student's result |
| **Compromised invigilator device** | Can intercept pre-signature image; cannot forge a signature without the device key | Alter images before signing |
| **Insider (exam manager / administrator)** | High system access; knows infrastructure | Cover-up fraud, alter results at scale |
| **Insider (database administrator)** | Direct PostgreSQL access | Modify scores, OCR results, or delete audit log entries |
| **Insider (storage administrator)** | Direct MinIO/S3 access | Replace or delete stored images |
| **External attacker** | Network access only; no credentials | Exfiltrate images; disrupt exam day; ransom |
| **Compromised OCR/Scoring service** | Service-level API access | Return fraudulent scores; exfiltrate images |
| **Nation-state / forensic adversary** | May obtain decrypted disk images; advanced cryptanalysis | Deniable result manipulation |

---

## 5. STRIDE Analysis

### 5.1 Mobile App (Flutter — Invigilator)

| Threat | STRIDE | Description | Mitigation |
|---|---|---|---|
| Device key extraction | **S** | Attacker extracts device private key to forge signatures | Keys stored in Secure Enclave / Android Keystore; never exported |
| Image substitution (pre-sign) | **T** | Malicious app substitutes a different image before hashing | Hash is computed on the raw camera frame before any manipulation; open-source app verifiable |
| Replay of a valid capture | **T** | Replay a previously signed (captureId, hash, signature) tuple | Server checks captureId uniqueness; duplicate upload returns 409 |
| Offline capture queue tampering | **T** | Attacker modifies SQLite queue on a rooted device | SQLite integrity is secondary — server re-verifies hash+signature on upload |
| Capture without registration | **S** | Unregistered device attempts upload | Device must be in `Devices` table with `status=Approved`; signature verify fails for unknown public key |
| MitM on upload | **I** | Attacker intercepts image bytes in transit | Mandatory TLS 1.3; certificate pinning recommended for production |

### 5.2 ExamShield API

| Threat | STRIDE | Description | Mitigation |
|---|---|---|---|
| JWT forgery | **S** | Attacker forges a JWT to impersonate a privileged role | JWT signed with ECDSA P-256; asymmetric key not exposed; short TTL |
| Privilege escalation via role claim | **E** | Attacker modifies JWT `role` claim | JWT signature verification on every request; role is server-issued only |
| Image replacement via PUT | **T** | Attacker uploads a different image to an existing captureId | No UPDATE path exists for image bytes; `/upload` only accepted once per captureId (409 on second attempt) |
| Mass extraction of answer images | **I** | Attacker with a valid token iterates all capture IDs | `ImageViewer` policy allowlist (6 roles); invigilator scope limits to own captures; rate limiting |
| Hash collision attack | **T** | Attacker engineers a different image with the same SHA-256 hash | SHA-256 collision is computationally infeasible; watermark further ties image to metadata |
| Audit log tampering | **T** | Attacker deletes or modifies audit entries | Append-only repository; no DELETE/UPDATE exists in domain; DB user has INSERT only on audit tables |
| SSRF via webhook alert URL | **T / I** | Admin configures a webhook URL pointing to internal services | Webhook URLs should be validated against an allowlist of external schemes; SSRF mitigations required |
| DoS via bulk OCR trigger | **D** | Attacker triggers OCR for thousands of captures simultaneously | Rate limiting on `/ocr/batch`; RabbitMQ queue depth cap; HPA scales OCR pods |
| Broken object-level auth | **E** | Invigilator accesses another invigilator's captures | `InvigilatorId` scope enforced on `GET /captures` and `GET /captures/{id}/image` |
| MFA bypass | **E** | InvestigationOfficer endpoint accessed without MFA JWT | `HasClaim("amr","mfa")` check on image endpoint; returns 403 if claim absent |
| Refresh token theft | **S** | Attacker steals a refresh token from localStorage | Short TTL; rotation on every use; revocation on logout; HttpOnly + SameSite=Strict cookie (dashboard); SecureStorage (mobile) |
| Public verify leaks PII | **I** | Anonymous verify endpoint exposes student PII | Response contains only verification status + timestamps; studentId and name are omitted |

### 5.3 Database (PostgreSQL)

| Threat | STRIDE | Description | Mitigation |
|---|---|---|---|
| DBA alters score rows | **T** | Privileged DB user updates `Scores` table directly | Score values are also embedded in signed audit log entries; discrepancy is detectable |
| DBA deletes audit log | **T** | Privileged DB user truncates `AuditLog` | Audit log should be replicated to a separate write-only sink (e.g., S3 + CloudWatch Logs) |
| SQL injection | **T / I** | Malicious input crafts unintended queries | EF Core parameterised queries throughout; no raw SQL with user input |
| Credential brute-force | **S** | Attacker attempts passwords against DB port | PostgreSQL not exposed to public network; only API pods can connect |
| Encrypted DEK exposure | **I** | Attacker reads `Captures.EncryptedDek` column | Column is AES-256-GCM ciphertext; useless without master key |

### 5.4 Object Storage (MinIO / S3)

| Threat | STRIDE | Description | Mitigation |
|---|---|---|---|
| Direct object download | **I** | Attacker with storage credentials downloads objects | Objects are AES-256-GCM ciphertext; plaintext requires the master key |
| Object deletion | **T** | Attacker deletes stored images | S3 Object Lock COMPLIANCE mode prevents deletion for the configured retention period |
| Object replacement | **T** | Attacker overwrites an object with a different image | Object Lock COMPLIANCE mode prevents any overwrite |
| Storage credential exposure | **I** | MinIO root credentials leaked | Rotate immediately; use IAM roles / instance profiles in prod; never commit credentials |

### 5.5 OCR & Scoring Services (System Roles)

| Threat | STRIDE | Description | Mitigation |
|---|---|---|---|
| Compromised OCR pod returns false answers | **T** | Pod is compromised and returns fabricated answer extraction | OCR results feed manual review for low-confidence entries; reviewer sees the original image independently |
| OCR pod exfiltrates images | **I** | Compromised pod copies decrypted bytes to external host | Network egress policies (Kubernetes `NetworkPolicy`); mTLS between pods |
| Score manipulation by Scoring Engine | **T** | Service returns wrong scores | Scores are immutable once written; answer key is compared server-side, not on the scoring pod |

---

## 6. Security Invariants (Non-Negotiable Controls)

These controls implement the mitigations above and must never be removed or bypassed:

| # | Invariant | Implementation |
|---|---|---|
| 1 | **Immutability** | No UPDATE/DELETE path in domain for images or audit entries; S3 Object Lock COMPLIANCE |
| 2 | **Hash-then-sign** | SHA-256 on raw bytes on-device before any network call; server re-verifies on receipt |
| 3 | **Digital signatures** | ECDSA P-256 per-capture signature; server verifies against registered device public key |
| 4 | **Invisible watermark** | LSB steganography (captureId + examId + nonce + hash + timestamp) in every stored image |
| 5 | **Read-only review** | No write path to image bytes for any reviewer role; `ImageViewer` policy is an allowlist |
| 6 | **Chain of custody** | Every state transition appended to immutable `AuditLog`; digitally signed |
| 7 | **Encryption at rest** | AES-256-GCM per-image DEK; envelope-encrypted master key via KMS in prod |
| 8 | **Image access allowlist** | `ImageViewer` policy: 6 roles only; admin and auditor roles are explicitly excluded |

---

## 7. Risk Register

| ID | Threat | Likelihood | Impact | Residual Risk | Owner |
|---|---|---|---|---|---|
| T-01 | Malicious invigilator substitutes image pre-hash | Low | Critical | **Low** — hash computed on raw frame; signature binds to device | Security Team |
| T-02 | Insider DBA modifies scores directly | Low | Critical | **Medium** — audit log detects discrepancy but requires out-of-band comparison | DBA + Auditor |
| T-03 | Master key leaked | Very Low | Critical | **Low** — prod key in KMS; rotation procedure documented | Security Admin |
| T-04 | JWT signing key compromised | Very Low | Critical | **Low** — ECDSA asymmetric; private key in secrets manager | Security Admin |
| T-05 | Answer image mass exfiltration via API | Low | High | **Low** — allowlist policy + invigilator scope + rate limiting + MFA step-up |  |
| T-06 | DoS on exam day (bulk upload flood) | Medium | High | **Medium** — rate limiting + HPA; DDoS mitigation at edge recommended | DevOps |
| T-07 | Refresh token theft from browser | Medium | Medium | **Medium** — short TTL + rotation; HttpOnly cookie recommended for prod dashboard | Dev Team |
| T-08 | OCR pod compromise + result fabrication | Very Low | High | **Low** — reviewer sees original image independently; results immutable once approved | Security Team |
| T-09 | Webhook SSRF | Low | Medium | **Medium** — URL allowlist not yet enforced; remediation tracked | Dev Team |
| T-10 | Audit log deletion by DBA | Very Low | Critical | **Medium** — no app-level deletion path; recommend append-only replica to S3 | Security Team |

---

## 8. Open Remediation Items

| ID | Item | Priority |
|---|---|---|
| ~~R-01~~ | ~~Enforce webhook URL allowlist (prevent SSRF)~~ | ~~High~~ — **Fixed**: `IsSafeWebhookUrl` in `NotificationChannelSettings` enforces HTTPS-only + rejects loopback / RFC-1918 / link-local (169.254.x.x). 17 new unit tests. |
| ~~R-02~~ | ~~Replicate audit log append-only to S3/CloudWatch~~ | ~~High~~ — **Fixed**: `ObjectStoreAuditLogArchiveService` writes each entry as `audit/{yyyy}/{MM}/{dd}/{id}.json` to a dedicated `examshield-audit` bucket (MinIO + Object Lock in dev; S3 + Object Lock in prod; Azure Blob + Immutable Blob Storage policy in Azure) after every `AuditLogRepository.AppendAsync`. Archive failures are swallowed so they can never block the primary database write. `NullAuditLogArchiveService` used for InMemory environments. 7 new unit tests. |
| ~~R-03~~ | ~~Certificate pinning for Flutter app in production~~ | ~~Medium~~ — **Fixed**: `PinnedHttpClientFactory.createPinned()` uses `SecurityContext(withTrustedRoots: false)` + `setTrustedCertificatesBytes()` to trust only the bundled `assets/certs/api_ca.pem`. `NetworkSecurity.createHttpClient()` switches automatically (pinned in `kReleaseMode`, development client otherwise). `main.dart` wires the pinned client into `ApiClient`. `CertificatePin` value object provides SHA-256 fingerprint verification. 11 new Flutter unit tests. |
| ~~R-04~~ | ~~HttpOnly + SameSite=Strict cookie for dashboard refresh token~~ | ~~Medium~~ — **Fixed**: `SetRefreshTokenCookie` in `AuthEndpoints` sets `HttpOnly; Secure; SameSite=Strict; Path=/auth; MaxAge=7d`; dashboard `client.ts` uses `credentials: 'include'`; `auth_refresh_token` removed from localStorage. 3 new integration tests. |
| R-05 | External penetration test before first national exam | High |
| R-06 | Formal HSM (FIPS 140-2 Level 3) for master key in highest-stakes deployments | Low |

---

## 9. Security Review Checklist

Use this before each production deployment:

- [ ] Master encryption key is stored in KMS/Vault — not in `appsettings.Production.json`
- [ ] JWT signing key is rotated and stored in secrets manager
- [ ] All device public keys have been audited — no unexpected `Approved` devices
- [ ] S3 bucket Object Lock COMPLIANCE mode is confirmed active
- [ ] Rate limiting thresholds are appropriate for expected invigilator count
- [ ] Audit log replication to off-cluster sink is active
- [ ] All privileged role accounts have MFA enrolled
- [ ] Network policies block OCR/Scoring pod egress to external hosts
- [ ] SSRF mitigations applied to webhook URL inputs
- [ ] `appsettings.Testing.json` and `appsettings.Development.json` are not deployed to production
- [ ] Penetration test findings from previous cycle are resolved or accepted
