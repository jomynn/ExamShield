# ExamShield Roadmap

This is the living roadmap for ExamShield Community Edition. Items may shift based on community feedback and contributor availability.

> Want to influence priorities? Open a [GitHub Discussion](https://github.com/your-org/examshield/discussions) or upvote existing issues.

---

## Legend

| Symbol | Meaning |
|---|---|
| ✅ | Done |
| 🚧 | In progress |
| 📋 | Planned |
| 💡 | Under consideration |
| ❌ | Descoped |

---

## v0.1 — Core Pipeline ✅ Complete

Goal: end-to-end capture → verify → audit with a single invigilator.

| Feature | Status |
|---|---|
| Flutter capture app (camera, document guide overlay, perspective correction, image enhancement) | ✅ |
| On-device SHA-256 hash generation | ✅ |
| ECDSA P-256 digital signature (on-device) | ✅ |
| `POST /capture` endpoint (register hash + signature) | ✅ |
| `POST /upload` endpoint (upload image, verify hash) | ✅ |
| Server-side signature verification | ✅ |
| Immutable MinIO storage (Object Lock, COMPLIANCE mode) | ✅ |
| Append-only audit log | ✅ |
| `GET /audit` query endpoint + chain-of-custody | ✅ |
| `GET /verify/{id}` endpoint | ✅ |
| Docker Compose dev stack | ✅ |
| Unit tests — domain + application layer | ✅ 1,309 |
| Integration tests — API + repository | ✅ 520 |

---

## v0.2 — OCR + Manual Review ✅ Complete

Goal: automated answer extraction with a human fallback queue.

| Feature | Status |
|---|---|
| Python OMR microservice (FastAPI + OpenCV bubble detection) | ✅ |
| `POST /ocr` trigger endpoint + batch + queue endpoints | ✅ |
| Confidence threshold configuration (`OcrConfidence.LowThreshold = 0.85`) | ✅ |
| Low-confidence routing to review queue | ✅ |
| Manual Review UI (two-panel: original image + OCR predictions) | ✅ |
| Pixel Lock — read-only ImageViewer, no image editing | ✅ |
| Every review decision creates immutable audit record | ✅ |
| Review Supervisor approve / reject / escalate workflow | ✅ |
| RabbitMQ message bus + Dead-Letter Queue + retry-count header | ✅ |

---

## v0.3 — Scoring & Result Publication ✅ Complete

Goal: end-to-end pipeline from capture to published result.

| Feature | Status |
|---|---|
| Scoring engine (`ScoreCapture`, `BatchScore`, rankings) | ✅ |
| `POST /score`, `GET /results`, `/results/publish` | ✅ |
| Result Publisher role workflow | ✅ |
| Student result portal (results table + dispute request) | ✅ |
| Public QR / hash / signature verification page | ✅ |
| PDF certificate generation (QuestPDF A4 landscape) | ✅ |
| `GET /statistics`, `/dashboard/stats` | ✅ |

---

## v0.4 — Full RBAC + MFA ✅ Complete

Goal: production-grade access control with Zero Trust enforcement.

| Feature | Status |
|---|---|
| All 15 roles fully implemented with JWT claims + RBAC policies | ✅ |
| JWT + Refresh Token auth | ✅ |
| MFA (TOTP) for privileged roles — `TotpService`, `MfaLoginCommandHandler` | ✅ |
| OAuth2 / OpenID Connect integration (Google + Azure AD providers) | ✅ |
| Device registration + certificate lifecycle | ✅ |
| Session management (list, revoke) | ✅ |
| Separation of Duties (ImageViewer policy, invigilator scope, MFA step-up) | ✅ |
| Role management UI (tree view + permission matrix grid) | ✅ |
| Fine-grained resource authorization | ✅ |

---

## v0.5 — Alerts & Security Center ✅ Complete

Goal: real-time security monitoring for examination staff.

| Feature | Status |
|---|---|
| Alert triggers: hash mismatch, duplicate upload, invalid signature | ✅ |
| Email (MailKit/SMTP) alert channel | ✅ |
| LINE Notify alert channel | ✅ |
| Slack alert channel | ✅ |
| Microsoft Teams alert channel | ✅ |
| Configurable webhook alert channel | ✅ |
| Dynamic channel settings (DB-driven, hot-reload per tenant) | ✅ |
| Security Center UI — event log, severity chips, active sessions | ✅ |
| Threat timeline chart (recharts AreaChart, last 24 h by severity) | ✅ |
| Audit Center UI — append-only timeline, export PDF/CSV/Excel | ✅ |

---

## v1.0 — Production-Ready ✅ Complete

Goal: deployable to real national-scale examinations.

| Feature | Status |
|---|---|
| Kubernetes manifests (api, postgres, redis, rabbitmq, minio, ocr-service, namespace) | ✅ |
| Horizontal pod autoscaling (HPA for API × 2–10, OCR × 2–8) | ✅ |
| AWS S3 Object Lock storage adapter | ✅ |
| Azure Immutable Blob storage adapter | ✅ |
| Invisible watermark — LSB steganography (SkiaSharp, blue-channel pixel embedding) | ✅ |
| HSM / KMS key management — config / Vault Transit / AWS KMS / Azure Key Vault adapters | ✅ |
| Full CI/CD pipeline (GitHub Actions — .NET, Dashboard, OCR service, Docker push) | ✅ |
| k6 load testing scripts (capture-upload, concurrent-review, ocr-pipeline scenarios) | ✅ |
| OpenAPI documentation + Scalar UI (DeepSpace theme) | ✅ |
| Deployment guide (`DEPLOYMENT.md`) | ✅ |
| Multi-region deployment support | 💡 Community interest welcome |
| Penetration testing + security audit | 📋 Engage an external firm |
| 99.9% uptime SLA architecture | 📋 Requires multi-region + chaos testing |
| User manuals (invigilator / admin / reviewer) | 📋 |

---

## Post-v1.0 — Under Consideration

These are ideas not yet committed to any milestone. Open a Discussion to advocate for one.

| Idea | Notes |
|---|---|
| GraphQL API | Complementary to REST, not a replacement |
| Offline-first mobile with local DB sync | SQLite on device, sync on reconnect |
| ML-based handwriting recognition | Beyond bubble sheets — free-form answers |
| Blockchain-anchored audit log | Timestamping via Bitcoin or Ethereum OP_RETURN |
| QR-code anti-copy watermark | Visible QR on printed sheet, verified on upload |
| Multi-tenant SaaS mode | Multiple exam boards on a single cluster |
| Biometric invigilator verification | Face match at capture start |
| Federated learning OCR model | Train without exposing raw answer sheets |

---

## How to Contribute to the Roadmap

1. **Comment on a planned item** — find its GitHub issue and add your use case.
2. **Propose a new item** — open a [Discussion → Ideas](https://github.com/your-org/examshield/discussions/categories/ideas).
3. **Pick up a task** — see issues labeled [`good first issue`](https://github.com/your-org/examshield/labels/good%20first%20issue) or [`help wanted`](https://github.com/your-org/examshield/labels/help%20wanted).

This roadmap is updated after each milestone closes.
