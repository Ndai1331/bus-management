# Deploy HCS trên Baota Panel (`/www/server/panel/data/compose/`)

Bundle **standalone** — không cần clone full repo trên server. Cùng pattern với `cms`, `axis`:

```
/www/server/panel/data/compose/
├── cms/
├── axis/
├── hcs-data/          ← PostgreSQL, Redis, RabbitMQ, MinIO
└── hcs/                 ← Apps: Blazor, Gateway, Auth, microservices, Keycloak
```

## Kiến trúc HTL Tech (2 server)

| IP | Vai trò |
|----|---------|
| **10.17.227.58** | HCS compose (`hcs/`), Redis, RabbitMQ, Nginx, Keycloak container |
| **10.17.227.64** | Postgres + MinIO (axis/cms) — **không** chạy `hcs-data` nếu dùng shared |

Folder trên panel:

```
/www/server/panel/data/compose/
├── cms/ / axis/     ← .64 (Postgres + MinIO)
├── hcs-infra/       ← optional: Redis + Rabbit riêng (Baota project thứ 2)
└── hcs/             ← .58 — apps + Redis + Rabbit (khuyến nghị, một project)
```

Trước `./up.sh` trên **.58**: mở firewall `.64` cho `5432`, `9000` từ `.58`; tạo DB `hcs_*` trên Postgres `.64`.

## Một server vs hai server (tổng quát)

| Kịch bản | Folder | Ghi chú |
|----------|--------|---------|
| **Hai máy** (HTL Tech) | `hcs/` trên `.58` | Postgres/MinIO qua `HCS_POSTGRES_HOST=10.17.227.64` |
| **Một máy** | `hcs/` (+ optional `hcs-data/`) | `HCS_*_HOST=host.docker.internal`, wait `127.0.0.1` |

---

## Bước 1 — Copy bundle lên server

Từ máy dev (có repo):

```bash
cd services/HCS_web_free_license
./deploy/panel/install-on-server.sh root@<IP-server>
```

Hoặc copy thủ công:

```bash
rsync -av deploy/panel/hcs-data/ root@<IP>:/www/server/panel/data/compose/hcs-data/
rsync -av deploy/panel/hcs/       root@<IP>:/www/server/panel/data/compose/hcs/
```

Trên server:

```bash
chmod +x /www/server/panel/data/compose/hcs-data/up.sh
chmod +x /www/server/panel/data/compose/hcs/up.sh
```

---

## Bước 2 — `.env`

### `hcs-data/.env`

```bash
cd /www/server/panel/data/compose/hcs-data
cp .env.example .env
chmod 600 .env
nano .env
```

Điền mật khẩu Postgres/Redis/Rabbit/MinIO. `HCS_DATA_HOST` = IP mà **container apps** dùng để kết nối (thường IP LAN server).

### `hcs/.env`

```bash
cd /www/server/panel/data/compose/hcs
cp .env.example .env
chmod 600 .env
nano .env
```

**Copy cùng** mật khẩu Postgres/MinIO từ axis stack trên `10.17.227.64` (xem `.env.example`). PFX trong `./.hcs-certs/`.

Chỉnh WASM client nếu cần: `config/blazor-client.appsettings.json`.

---

## Bước 3 — Cert PFX (chỉ server `hcs`)

Đặt 2 file trong thư mục compose (không commit):

```
/www/server/panel/data/compose/hcs/.hcs-certs/
├── openiddict.pfx
└── dataprotection.pfx
```

Path trong `.env`: `HCS_OPENIDDICT_PFX=/www/server/panel/data/compose/hcs/.hcs-certs/openiddict.pfx`

---

## Bước 4 — Khởi động stack (trên **10.17.227.58**)

Postgres + MinIO trên `.64` (axis) — **không** chạy `hcs-data`. Redis + Rabbit **gộp** trong `hcs/docker-compose.yml`.

```bash
cd /www/server/panel/data/compose/hcs
./up.sh
```

**Tùy chọn** — tách Redis/Rabbit project Baota riêng:

```bash
cd /www/server/panel/data/compose/hcs-infra && ./up.sh
# hcs/.env: HCS_REDIS_HOST=host.docker.internal, HCS_RABBITMQ_HOST=host.docker.internal
cd /www/server/panel/data/compose/hcs && ./up.sh
```

Management UI RabbitMQ: `http://127.0.0.1:15672` (user `HCS_RABBITMQ_USER`).

### Qua Baota Panel UI

1. **Docker** → **Compose** → **Add project**
2. Project path: `/www/server/panel/data/compose/hcs-data` → compose file `docker-compose.yml`
3. Lặp lại với `/www/server/panel/data/compose/hcs`
4. Env file: panel thường đọc `.env` cùng thư mục nếu có

---

## Bước 5 — Nginx / SSL (4 domain)

Container bind loopback:

| Port | Service |
|------|---------|
| `127.0.0.1:8081` | Blazor |
| `127.0.0.1:8082` | Gateway / BFF |
| `127.0.0.1:8083` | AuthServer |
| `127.0.0.1:8084` | Keycloak |

**Cách A — Nginx system** (khuyến nghị, config đầy đủ):

```bash
sudo cp /path/to/repo/deploy/ubuntu/nginx/hcs-proxy.inc /etc/nginx/hcs-proxy.inc
sudo cp /path/to/repo/deploy/ubuntu/nginx/hcs.conf /etc/nginx/sites-available/hcs.conf
sudo ln -sfn /etc/nginx/sites-available/hcs.conf /etc/nginx/sites-enabled/hcs.conf
sudo nginx -t && sudo systemctl reload nginx
```

**Cách B — Baota Website:** tạo 4 site HTTPS, reverse proxy tới `127.0.0.1:8081–8084` theo bảng domain trong [`docs/runbooks/deploy-server.md`](../runbooks/deploy-server.md).

DNS A record: `hanhchinhso`, `api-hcs`, `auth-hcs`, `sso-hcs` → IP server apps.

---

## Bước 6 — Bootstrap Keycloak

Cần script bootstrap từ repo (một lần):

```bash
git clone <repo> /opt/hcs-src   # hoặc scp scripts/keycloak_bootstrap_bd_realm.py

export HCS_AUTH_CLIENT_SECRET='<khớp HCS_KEYCLOAK_CLIENT_SECRET trong hcs/.env>'
export HCS_AUTH_PUBLIC_HOST='auth-hcs.htltech.vn'
export KEYCLOAK_URL='https://sso-hcs.htltech.vn'
export KEYCLOAK_ADMIN='admin'
export KEYCLOAK_ADMIN_PASSWORD='<khớp .env>'

python3 /opt/hcs-src/scripts/keycloak_bootstrap_bd_realm.py
```

---

## Cập nhật sau CI (push Docker Hub)

```bash
cd /www/server/panel/data/compose/hcs
./up.sh
```

`up.sh` tự `docker compose pull` khi `HCS_PULL_IMAGES=true`.

---

## Tài liệu đầy đủ

- [`docs/runbooks/deploy-server.md`](../runbooks/deploy-server.md) — CI/CD, smoke test, troubleshooting
- [`deploy/ubuntu/nginx/hcs.conf`](../ubuntu/nginx/hcs.conf) — mẫu Nginx 4 vhost
