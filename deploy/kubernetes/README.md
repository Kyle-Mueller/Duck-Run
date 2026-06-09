# Control Dashboard — Kubernetes

Minimal manifests to get the dashboard running. Adjust storage class, ingress
controller, and TLS issuer to match your cluster.

| File | What it is |
|---|---|
| `duckrun-namespace.yaml` | The `duckrun` namespace. |
| `duckrun-secrets.example.yaml` | Template for the dashboard/Postgres secret. **Copy, edit, rename — do not apply as-is.** |
| `duckrun-postgres.yaml` | Evaluation Postgres (StatefulSet + headless Service). |
| `duckrun-dashboard.yaml` | The dashboard Deployment + Service. Image pinned to this release. |
| `duckrun-ingress.yaml` | Example nginx + cert-manager ingress. |

## Apply

```bash
# 1. Namespace
kubectl apply -f duckrun-namespace.yaml

# 2. Secrets — copy the example, fill it in, then apply YOUR file (not the example)
cp duckrun-secrets.example.yaml duckrun-secrets.yaml
#   ...edit duckrun-secrets.yaml...
kubectl apply -f duckrun-secrets.yaml

# 3. Database, dashboard, ingress
kubectl apply -f duckrun-postgres.yaml
kubectl apply -f duckrun-dashboard.yaml
kubectl apply -f duckrun-ingress.yaml
```

## Notes

- The image is pinned to this release version. To track the newest build, change `image:` to `:latest` (and set `imagePullPolicy: Always`).
- For production, prefer a managed Postgres or an operator (CloudNativePG, Zalando) over the bundled StatefulSet.
- gRPC ingest (HTTP/2) works through nginx-ingress. If you put another proxy in front, make sure it allows end-to-end HTTP/2.
- **SignalR** (live console) needs long-lived WebSocket connections — the `proxy-read-timeout` annotation handles this for nginx. Behind Cloudflare or similar, enable WebSockets.
- Health endpoints: `/health/live` (liveness) and `/health/ready` (readiness — checks the DB).
