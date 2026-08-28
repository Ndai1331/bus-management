# Deploy HCS lên server HTL Tech (Docker Hub + Ubuntu 24)

Hướng dẫn triển khai production cho **HCS Community**: data tách server, image pull từ Docker Hub, Keycloak SSO, Nginx + Let's Encrypt (**HTTPS mặc định**).

> Lab một máy (`docker-compose.yml` + Caddy + `hcs.localhost`): xem [`../../docs/runbooks/hcs-docker-compose-handoff.md`](../../docs/runbooks/hcs-docker-compose-handoff.md).

## Domain production (HTL Tech)

| Biến `.env` | Domain | Dịch vụ |
|-------------|--------|---------|
| `HCS_PUBLIC_HOST` | **hanhchinhso.htltech.vn** | Blazor UI |
| `HCS_API_PUBLIC_HOST` | **api-hcs.htltech.vn** | Gateway / BFF / `/api` / SignalR |
| `HCS_AUTH_PUBLIC_HOST` | **auth-hcs.htltech.vn** | OpenIddict AuthServer |
| `HCS_SSO_PUBLIC_HOST` | **sso-hcs.htltech.vn** | Keycloak |

Blazor và Gateway **khác host** → bắt buộc `Bff__CookieDomain=htltech.vn` (cookie `.HCS.Bff` dùng chung parent domain).

Mẫu `.env` sẵn: [`deploy/ubuntu/.env.example`](../../deploy/ubuntu/.env.example)  
Cấu hình WASM client (API URL): [`deploy/ubuntu/config/blazor-client.appsettings.json`](../../deploy/ubuntu/config/blazor-client.appsettings.json)

## Kiến trúc

| Server | Vai trò |
|--------|---------|
| **Data** (`HCS_DATA_HOST`) | PostgreSQL, Redis (password), RabbitMQ, MinIO |
| **Apps** (`HCS_APPS_HOST`) | Nginx, Keycloak, 9 container HCS, DbMigrator |

### Baota Panel (`/www/server/panel/data/compose/`)

Nếu server dùng **Baota** và đã có `cms`, `axis` cùng pattern compose:

```
/www/server/panel/data/compose/
├── cms/
├── axis/
├── hcs-data/    ← Postgres, Redis, RabbitMQ, MinIO
└── hcs/         ← Blazor, Gateway, Auth, microservices, Keycloak
```

Bundle standalone (không cần clone repo): [`deploy/panel/README.md`](../../deploy/panel/README.md)

```bash
# Từ máy dev
./deploy/panel/install-on-server.sh root@<IP-server>
```

Trên server: copy `.env.example` → `.env` ở cả hai folder, chạy `hcs-data/up.sh` rồi `hcs/up.sh`.

**4 DNS A record** trỏ về server Apps (cùng IP).

Luồng đăng nhập:

1. User → `https://hanhchinhso.htltech.vn`
2. Redirect login → `https://api-hcs.htltech.vn/bff/login`
3. OIDC → `https://auth-hcs.htltech.vn/connect/authorize`
4. SSO → `https://sso-hcs.htltech.vn`
5. Callback OIDC → `https://api-hcs.htltech.vn/signin-oidc` → về Blazor

Chi tiết mạng/UFW: [`hcs-ubuntu24-two-server.md`](./hcs-ubuntu24-two-server.md).

---

## Phần A — CI/CD (GitHub → Docker Hub)

Mỗi lần **push lên `main`**, GitHub Actions build và push 9 image:

| Tag | Service |
|-----|---------|
| `longnguyen1331/hanhchinhso:db-migrator` | DbMigrator |
| `longnguyen1331/hanhchinhso:auth-server` | AuthServer |
| `longnguyen1331/hanhchinhso:web-gateway` | BFF / Gateway |
| `longnguyen1331/hanhchinhso:blazor` | Blazor UI |
| `longnguyen1331/hanhchinhso:platform` | Platform |
| `longnguyen1331/hanhchinhso:organization` | Organization |
| `longnguyen1331/hanhchinhso:document` | Document |
| `longnguyen1331/hanhchinhso:work-management` | Work Management |
| `longnguyen1331/hanhchinhso:collaboration` | Collaboration |

Workflow: [`.github/workflows/hcs-docker-publish.yml`](../../../.github/workflows/hcs-docker-publish.yml)

GitHub Secrets: `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`.

Có thể chạy thủ công workflow với input `services` là danh sách image phân cách bằng dấu cách, ví dụ `blazor auth-server gateway`. `gateway` là alias của tag `web-gateway`; dùng `all` để build toàn bộ image.

---

## Phần B — Deploy server (step by step)

### B.0 Checklist

- [ ] Ubuntu 24.04, Docker, NTP bật
- [ ] DNS: 4 domain → IP server Apps
- [ ] Image trên Docker Hub (`HCS_PULL_IMAGES=true`)

### B.1 Clone & `.env`

```bash
sudo mkdir -p /opt/hcs && sudo chown "$USER":"$USER" /opt/hcs
git clone <repo-url> /opt/hcs/bd-workspace
cd /opt/hcs/bd-workspace/services/HCS_web_free_license

cp deploy/ubuntu/.env.example deploy/ubuntu/.env
chmod 600 deploy/ubuntu/.env
nano deploy/ubuntu/.env
```

**`.env` production HTL Tech (điền secret thật):**

```bash
HCS_DATA_HOST=<IP-server-data>
HCS_APPS_HOST=<IP-server-apps>

HCS_PUBLIC_HOST=hanhchinhso.htltech.vn
HCS_API_PUBLIC_HOST=api-hcs.htltech.vn
HCS_AUTH_PUBLIC_HOST=auth-hcs.htltech.vn
HCS_SSO_PUBLIC_HOST=sso-hcs.htltech.vn

Bff__CookieDomain=htltech.vn

HCS_DOCKER_REGISTRY=longnguyen1331/hanhchinhso
HCS_PULL_IMAGES=true

HCS_POSTGRES_USER=hcs
HCS_POSTGRES_PASSWORD=<strong-password>
HCS_REDIS_PASSWORD=<strong-password>
HCS_RABBITMQ_USER=hcs-events
HCS_RABBITMQ_PASSWORD=<strong-password>
HCS_MINIO_ROOT_USER=hcs-minio
HCS_MINIO_ROOT_PASSWORD=<strong-password>

HCS_ADMIN_PASSWORD=<admin-seed-password>
HCS_GATEWAY_CLIENT_SECRET=<openiddict-hcs-app-secret>
HCS_KEYCLOAK_ADMIN=admin
HCS_KEYCLOAK_ADMIN_PASSWORD=<keycloak-admin-password>
HCS_KEYCLOAK_CLIENT_SECRET=<hcs-free-auth-secret>

HCS_OPENIDDICT_PFX=/etc/hcs/certs/openiddict.pfx
HCS_OPENIDDICT_PFX_PASSWORD=<pfx-password>
HCS_DATAPROTECTION_PFX=/etc/hcs/certs/dataprotection.pfx
HCS_DATAPROTECTION_PFX_PASSWORD=<pfx-password>
```

Copy **cùng** mật khẩu Postgres/Redis/Rabbit/MinIO sang `.env` trên server Data.

**Blazor WASM client** — chỉnh nếu domain khác mẫu:

`deploy/ubuntu/config/blazor-client.appsettings.json`:

```json
{
  "RemoteServices": { "Default": { "BaseUrl": "https://api-hcs.htltech.vn/" } },
  "Bff": {
    "PublicOrigin": "https://api-hcs.htltech.vn",
    "AccountUrl": "https://auth-hcs.htltech.vn/Account/Manage"
  }
}
```

File này được mount vào container Blazor khi `up-apps.sh`.

### B.2 TLS Let's Encrypt (4 SAN)

```bash
sudo apt-get install -y certbot python3-certbot-dns-cloudflare
sudo certbot certonly --dns-cloudflare \
  --dns-cloudflare-credentials /root/.secrets/cloudflare.ini \
  -d hanhchinhso.htltech.vn \
  -d api-hcs.htltech.vn \
  -d auth-hcs.htltech.vn \
  -d sso-hcs.htltech.vn
```

Cert live thường tại `/etc/letsencrypt/live/hanhchinhso.htltech.vn/` — khớp `deploy/ubuntu/nginx/hcs.conf`.

### B.3 PFX OpenIddict + Data Protection

```bash
sudo mkdir -p /etc/hcs/certs && sudo chmod 750 /etc/hcs/certs
# (xem lệnh openssl đầy đủ trong hcs-ubuntu24-two-server.md §3.2)
```

### B.4 Data stack (server Data)

```bash
./deploy/ubuntu/up-data.sh
```

### B.5 Nginx (server Apps) — trước khi smoke HTTPS

```bash
sudo apt-get install -y nginx
sudo cp deploy/ubuntu/nginx/hcs-proxy.inc /etc/nginx/hcs-proxy.inc
sudo cp deploy/ubuntu/nginx/hcs.conf /etc/nginx/sites-available/hcs.conf
sudo ln -sfn /etc/nginx/sites-available/hcs.conf /etc/nginx/sites-enabled/hcs.conf
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl enable --now nginx
```

| Host | Nginx proxy |
|------|-------------|
| `hanhchinhso.htltech.vn` | Blazor (`8081`); `/Account/*` → auth domain |
| `api-hcs.htltech.vn` | Gateway (`8082`): `/bff/`, `/api/`, `/hubs/`, OIDC callback |
| `auth-hcs.htltech.vn` | AuthServer (`8083`) |
| `sso-hcs.htltech.vn` | Keycloak (`8084`) |

### B.6 Khởi động Apps + Keycloak

```bash
docker login   # nếu Hub private
./deploy/ubuntu/up-apps.sh
```

Compose set (HTTPS):

| Container | Biến quan trọng |
|-----------|-----------------|
| db-migrator | `OpenIddict__Applications__HCS_App__RootUrl=https://api-hcs.htltech.vn` |
| | `PostLogoutRootUrl=https://hanhchinhso.htltech.vn` |
| web-gateway | `Bff__PublicOrigin=https://api-hcs.htltech.vn`, `Bff__CookieDomain=htltech.vn` |
| blazor | `App__SelfUrl=https://hanhchinhso.htltech.vn`, `Bff__PublicOrigin=https://api-hcs.htltech.vn` |
| auth-server | `Authentication__Keycloak__Authority=https://sso-hcs.htltech.vn/realms/bd` |
| keycloak | `KC_HOSTNAME=https://sso-hcs.htltech.vn` |

### B.7 Bootstrap Keycloak realm `bd`

Sau khi Keycloak healthy và Nginx proxy `https://sso-hcs.htltech.vn`:

```bash
cd /opt/hcs/bd-workspace

export HCS_AUTH_CLIENT_SECRET='<khớp HCS_KEYCLOAK_CLIENT_SECRET>'
export HCS_AUTH_PUBLIC_HOST='auth-hcs.htltech.vn'
export KEYCLOAK_URL='https://sso-hcs.htltech.vn'
export KEYCLOAK_ADMIN='admin'
export KEYCLOAK_ADMIN_PASSWORD='<khớp .env>'

python3 scripts/keycloak_bootstrap_bd_realm.py
```

Kiểm tra:

```bash
curl -s https://sso-hcs.htltech.vn/realms/bd/.well-known/openid-configuration \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['issuer'])"
# https://sso-hcs.htltech.vn/realms/bd
```

Client `hcs-free-auth`:

- Redirect: `https://auth-hcs.htltech.vn/signin-oidc`
- Secret = `HCS_KEYCLOAK_CLIENT_SECRET`

OpenIddict client `HCS_App` (tự seed DbMigrator):

- Callback: `https://api-hcs.htltech.vn/signin-oidc`
- Post-logout: `https://hanhchinhso.htltech.vn`

### B.8 Smoke test

```bash
curl -I https://hanhchinhso.htltech.vn/
curl -I https://api-hcs.htltech.vn/bff/login
curl -I https://auth-hcs.htltech.vn/
curl -I https://sso-hcs.htltech.vn/
```

Trình duyệt:

1. `https://hanhchinhso.htltech.vn` → login
2. **Login với SSO** → `https://sso-hcs.htltech.vn`
3. User có group `bd-app-hcs` (ví dụ `admin@benhvien.vn` / `Passw0rd!`)

---

## Phần C — Cập nhật sau CI

```bash
cd /opt/hcs/bd-workspace/services/HCS_web_free_license
./deploy/ubuntu/up-apps.sh
```

---

## Phần D — Sự cố

| Triệu chứng | Hướng xử lý |
|-------------|-------------|
| Login loop / không giữ session | Kiểm tra `Bff__CookieDomain=htltech.vn`; cookie `Secure` + HTTPS |
| API 401 từ Blazor | `blazor-client.appsettings.json` phải trỏ `api-hcs.htltech.vn` |
| OIDC redirect mismatch | OpenIddict `RootUrl` = API host; Keycloak redirect = auth host |
| CORS error | Gateway `App__CorsOrigins` gồm Blazor + API origin |
| JWT invalid issuer | Cert SAN đủ 4 domain; `extra_hosts` trong compose |

---

## Tài liệu liên quan

- [`deploy/panel/README.md`](../../deploy/panel/README.md) — Baota Panel, folder `hcs-data` + `hcs`
- [`deploy/ubuntu/.env.example`](../../deploy/ubuntu/.env.example)
- [`hcs-ubuntu24-two-server.md`](./hcs-ubuntu24-two-server.md)
- [`../../docs/runbooks/hcs-docker-compose-handoff.md`](../../docs/runbooks/hcs-docker-compose-handoff.md)
