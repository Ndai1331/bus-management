# Ubuntu 24 two-server deploy

Copy [`.env.example`](./.env.example) to `.env` on both hosts. Follow the runbook:

[`docs/runbooks/hcs-ubuntu24-two-server.md`](../../docs/runbooks/hcs-ubuntu24-two-server.md)

| Server | Compose | Entrypoint |
|--------|---------|------------|
| `10.17.227.64` | [`docker-compose.data.yml`](./docker-compose.data.yml) | [`up-data.sh`](./up-data.sh) |
| `10.17.227.58` | [`docker-compose.apps.yml`](./docker-compose.apps.yml) | [`up-apps.sh`](./up-apps.sh) |

Host Nginx files: [`nginx/hcs.conf`](./nginx/hcs.conf) and [`nginx/hcs-proxy.inc`](./nginx/hcs-proxy.inc).
