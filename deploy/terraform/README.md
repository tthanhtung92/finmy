# Terraform: Hetzner VPS, firewall and DNS

This configuration has **never been applied against a real Hetzner account**.
It is written and `terraform validate`-clean (confirmed against provider
schemas: `hetznercloud/hcloud` ~> 1.50, `germanbrew/hetznerdns` ~> 3.5), but
no server exists yet. "Deployed and reachable over HTTPS" stays unchecked in
`ROADMAP.md` section 6 until someone runs `terraform apply` here for real.

## What it creates

- One `hcloud_server` (default `cx22`, `nbg1`, Ubuntu 24.04), with a
  `cloud-init.yaml` user-data script that installs k3s (Traefik, local-path
  and metrics-server left on their bundled defaults) and drops cert-manager
  plus a Let's Encrypt `ClusterIssuer` in as manifests k3s auto-applies, with
  no separate Helm install step needed.
- One `hcloud_firewall`: 22 and 6443 from `admin_cidr` only, 80 and 443 open.
- One `hetznerdns_record` A and AAAA pointing `hostname` at the server.

State is local: there is no `backend` block, on purpose, since nothing has
been applied yet. Pick a real backend (Hetzner Object Storage, or anything
else) before the first real `terraform apply`, not after.

## Rough cost

A `cx22` runs a few euros a month; the DNS zone is free if it already exists
in the Hetzner account. No managed database, load balancer, or block storage
is provisioned; Postgres, Redis and MinIO live on the server's own disk (see
[ADR-0018](../../docs/adr/0018-self-hosted-deployment-shape.md)).

## Running it for the first time

```bash
cp terraform.tfvars.example terraform.tfvars   # fill in real values, gitignored
terraform init
terraform plan
terraform apply
```

After `apply` succeeds, one manual step this configuration deliberately does
not automate: fetch `/etc/rancher/k3s/k3s.yaml` from the server, rewrite its
`server:` field from `https://127.0.0.1:6443` to the public FQDN, and put the
result in the `KUBE_CONFIG` GitHub Actions secret (base64-encoded) so
`release.yml`'s `deploy` job can reach the cluster. Automating this would mean
handing Terraform a live cluster credential to manage, which is a bigger
blast radius than the one-time copy it replaces.
