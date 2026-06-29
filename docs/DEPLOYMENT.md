# ExamShield — Deployment Guide

## Overview

ExamShield ships as a self-contained deployment package built by `package.sh` / `package.ps1`.
The package includes the API Docker image, compiled dashboard, nginx config, production Compose file,
Kubernetes manifests, and a secrets template — everything needed to stand up a production instance.

---

## Package contents

```
examshield-VERSION/
├── api-image.tar              Docker image (gzip-compressed)
├── docker-compose.prod.yml    Single-server production Compose
├── dashboard/                 Compiled React SPA (Vite build)
├── nginx/
│   └── nginx.conf             Serves dashboard + proxies /api/* → API container
├── k8s/                       Kubernetes manifests (image tag pre-stamped)
├── .env.template              Secrets template with all required variables
└── DEPLOY.md                  Operator quick-start (also bundled in the package)
```

---

## Building a package

### Prerequisites

| Tool | Min version | Notes |
|------|-------------|-------|
| Docker | 24+ | Required to build and save the API image |
| .NET SDK | 9.0+ | Required to run tests and publish |
| Node.js | 20+ | Required to build the React dashboard |
| git | any | Used to read the version from the current tag |

### macOS / Linux

```bash
# Tag the release commit first
git tag v1.2.3

# Build tarball only
./package.sh

# Build tarball AND push to a container registry
./package.sh --push ghcr.io/yourorg

# Override version without a tag
./package.sh --version 1.2.3

# Skip tests (e.g. in a CI re-run after passing tests on a previous step)
./package.sh --skip-tests
```

### Windows (PowerShell)

```powershell
.\package.ps1
.\package.ps1 -Push ghcr.io/yourorg
.\package.ps1 -Version 1.2.3
.\package.ps1 -SkipTests
```

### Output

```
dist/
├── examshield-VERSION.tar.gz        deployment package
└── examshield-VERSION.tar.gz.sha256 SHA-256 checksum
```

---

## Versioning

Version is read automatically from the current git tag using `git describe --tags`.

| Git state | Version used |
|-----------|-------------|
| Exact tag (`v1.2.3`) | `1.2.3` |
| Commits after tag | `1.2.3-5-gabcd1234` |
| No tags at all | `0.0.0-dev-abcd1234` |
| `--version 1.0.0` flag | `1.0.0` (override) |

---

## Deployment: Docker Compose (single server)

### Step 1 — Transfer and extract

```bash
scp examshield-1.2.3.tar.gz user@server:/opt/examshield/
ssh user@server
cd /opt/examshield
tar -xzf examshield-1.2.3.tar.gz
cd examshield-1.2.3
```

### Step 2 — Configure secrets

```bash
cp .env.template .env
nano .env    # fill in every CHANGE_ME value
```

Generate all required secrets securely:

```bash
# PostgreSQL
printf "POSTGRES_PASSWORD=%s\n" "$(openssl rand -base64 24)" >> .env

# Redis
printf "REDIS_PASSWORD=%s\n"    "$(openssl rand -base64 24)" >> .env

# RabbitMQ
printf "RABBITMQ_PASSWORD=%s\n" "$(openssl rand -base64 24)" >> .env

# MinIO
printf "MINIO_SECRET_KEY=%s\n"  "$(openssl rand -base64 32)" >> .env

# JWT (must be at least 32 chars)
printf "JWT_SECRET=%s\n"        "$(openssl rand -base64 64)" >> .env

# AES-256 image encryption master key
printf "ENCRYPTION_MASTER_KEY=%s\n" "$(openssl rand -base64 32)" >> .env

# Watermark HMAC key
printf "WATERMARK_HMAC_KEY=%s\n"   "$(openssl rand -base64 32)" >> .env

# ECDSA server signing key (P-256)
openssl ecparam -name prime256v1 -genkey -noout | \
  openssl pkcs8 -topk8 -nocrypt > server-signing-key.pem
printf 'SERVER_SIGNING_KEY="%s"\n' "$(cat server-signing-key.pem | tr '\n' '\\n')" >> .env
```

> **Production key management:** Replace `ENCRYPTION_MASTER_KEY` with a reference to
> HashiCorp Vault Transit, AWS KMS, or Azure Key Vault before go-live.

### Step 3 — Load the Docker image

```bash
# If the image was saved to the package (no registry)
docker load < api-image.tar

# If using a registry (image already pushed by package.sh --push)
# Docker will pull automatically on compose up
```

### Step 4 — Start services

```bash
docker compose -f docker-compose.prod.yml up -d
```

Watch startup:

```bash
docker compose -f docker-compose.prod.yml logs -f
```

### Step 5 — First-time setup wizard

Once all containers report `healthy`, open a browser:

```
http://YOUR_SERVER/setup
```

The wizard:
1. Verifies connectivity to all backend services
2. Creates the first Super Administrator account
3. Optionally loads demo data for evaluation

> The wizard is permanently disabled once the first Super Administrator exists.

---

## Upgrading

```bash
# Extract the new package alongside the old one
tar -xzf examshield-1.3.0.tar.gz
cd examshield-1.3.0

# Re-use existing .env (secrets do not change between versions)
cp ../examshield-1.2.3/.env .

# Load new API image (skip if using a registry)
docker load < api-image.tar

# Rolling restart — database migrations apply automatically on API startup
docker compose -f docker-compose.prod.yml up -d --no-deps api
```

---

## Deployment: Kubernetes

The `k8s/` directory contains manifests for all services.
The API image tag is pre-stamped by `package.sh`.

### Step 1 — Fill in secrets

Edit `k8s/api.yaml` and replace every `CHANGE_ME` value in the `Secret` block
with base64-encoded production values:

```bash
echo -n "my-secret-value" | base64
```

### Step 2 — Apply manifests

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/
```

### Step 3 — Verify

```bash
kubectl -n examshield get pods
kubectl -n examshield logs deployment/api
```

---

## Architecture: single-server networking

```
Internet
    │
    ▼
┌──────────┐  port 80   ┌──────────────────────────────┐
│  nginx   │ ─────────► │  /          → dashboard/     │
│ container│            │  /api/*     → api:8080        │
└──────────┘            │  /hubs/*    → api:8080 (WS)  │
                        └──────────────────────────────┘
                                   │
                            Docker network (internal)
                         ┌─────────┴──────────────┐
                         ▼         ▼       ▼       ▼
                      postgres   redis  rabbitmq  minio
```

- All infrastructure containers are on an **internal** Docker network — no direct internet access.
- RabbitMQ management UI (port 15672) and MinIO console (port 9001) are bound to `127.0.0.1` only.
- Put a TLS-terminating reverse proxy (Caddy, Traefik, nginx, load balancer) in front of port 80 for HTTPS.

---

## Security checklist (before go-live)

- [ ] All `.env` secrets are randomly generated (not from `.env.template`)
- [ ] `ENCRYPTION_MASTER_KEY` stored in a secrets manager (Vault, KMS), not in `.env`
- [ ] `MINIO_ENABLE_LOCK=true` (tamper-evident WORM storage, cannot be undone)
- [ ] `Features__AutoSeedDemo=false` (default in production build)
- [ ] `Features__EnforceMfaForPrivilegedRoles=true` (default in production build)
- [ ] RabbitMQ (15672) and MinIO console (9001) NOT reachable from the internet
- [ ] TLS configured upstream; HTTP redirects to HTTPS
- [ ] Automated backups for `postgres_data` and `minio_data` Docker volumes
- [ ] Log aggregation connected (Loki, CloudWatch, etc.)
- [ ] Alerts configured in Settings → Notification Channels

---

## Environment variables reference

| Variable | Required | Description |
|----------|----------|-------------|
| `POSTGRES_PASSWORD` | Yes | PostgreSQL password |
| `REDIS_PASSWORD` | Yes | Redis `requirepass` |
| `RABBITMQ_PASSWORD` | Yes | RabbitMQ default user password |
| `MINIO_ACCESS_KEY` | Yes | MinIO root user |
| `MINIO_SECRET_KEY` | Yes | MinIO root password (min 32 chars) |
| `MINIO_BUCKET` | No | Bucket name (default: `examshield`) |
| `MINIO_ENABLE_LOCK` | No | Enable S3 Object Lock (default: `true`) |
| `JWT_SECRET` | Yes | JWT signing key (min 32 chars) |
| `JWT_ISSUER` | No | JWT issuer claim (default: `ExamShield`) |
| `JWT_AUDIENCE` | No | JWT audience claim (default: `ExamShield`) |
| `ENCRYPTION_MASTER_KEY` | Yes | AES-256 master key (base64, 32 bytes) |
| `WATERMARK_HMAC_KEY` | Yes | Watermark HMAC key (base64, 32 bytes) |
| `SERVER_SIGNING_KEY` | Yes | ECDSA P-256 private key PEM |
| `DASHBOARD_URL` | Yes | Dashboard origin for CORS (no trailing slash) |
| `DASHBOARD_PORT` | No | Host port for nginx (default: `80`) |
| `API_PORT` | No | Host port for API (default: `8080`) |
| `APP_VERSION` | No | Image tag to use (set by `package.sh`) |
| `OCR_TYPE` | No | `Stub` or `Http` (default: `Stub`) |
| `OCR_ENDPOINT` | No | OCR service URL (when `OCR_TYPE=Http`) |

---

## Related scripts

| Script | Purpose |
|--------|---------|
| [`setup.sh`](../setup.sh) | First-time developer setup (docker, migrations, env gen) |
| [`setup.ps1`](../setup.ps1) | Windows equivalent of `setup.sh` |
| [`package.sh`](../package.sh) | Build deployment tarball + optional registry push |
| [`package.ps1`](../package.ps1) | Windows equivalent of `package.sh` |
