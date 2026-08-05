# Deploy: Helm chart, Terraform, and encrypted secrets

## Layout

```text
deploy/
├── helm/finmy/              # the chart: API + in-cluster Postgres/Redis/MinIO + backup CronJob
├── values-prod.sops.yaml    # SOPS-encrypted secrets, committed
└── terraform/                # Hetzner VPS + firewall + DNS, written but never applied (see its own README)
```

## Secrets: SOPS + age

`deploy/values-prod.sops.yaml` holds the only values `helm/finmy/values.yaml`
ships as empty placeholders: `secrets.jwtSigningKey`, `secrets.postgresPassword`,
`secrets.minioRootUser`, `secrets.minioRootPassword`. Keys stay readable in
diffs; only the values are encrypted. `.sops.yaml` at the repo root points at
one age recipient. The corresponding private key lives only in the
`SOPS_AGE_KEY` GitHub Actions secret and in the repo owner's password manager,
never committed.

Deploying reads it like this:

```bash
export SOPS_AGE_KEY="AGE-SECRET-KEY-1..."   # from the password manager, not this repo
sops -d deploy/values-prod.sops.yaml > /tmp/secrets.yaml
helm upgrade --install finmy deploy/helm/finmy -f /tmp/secrets.yaml --set image.tag=<tag>
```

`.github/workflows/release.yml`'s `deploy` job does exactly this on tag.

### Rotating a value

```bash
sops deploy/values-prod.sops.yaml   # opens a decrypted buffer in $EDITOR, re-encrypts on save
```

### Generating a new key pair (only if the current one is lost)

```bash
age-keygen -o finmy-prod.agekey   # keep the private key out of the repo
```

Update `.sops.yaml`'s `age:` recipient with the new public key, then
`sops updatekeys deploy/values-prod.sops.yaml` to re-encrypt for it. Rotate
`SOPS_AGE_KEY` in Actions to match.

## Verifying the chart without a cluster

No `helm`, `kubectl`, `terraform`, `sops` or `age` binary is assumed to be on
a dev machine. `.github/workflows/ci.yml`'s `chart-and-infra` job is the
place these actually get checked:

```bash
helm lint deploy/helm/finmy
helm template finmy deploy/helm/finmy --set image.tag=test \
  --set secrets.jwtSigningKey=... --set secrets.postgresPassword=... \
  --set secrets.minioRootUser=... --set secrets.minioRootPassword=...
```

## What's out of scope this phase

The Ingress, cert-manager issuance, HPA scaling, PDB behavior, the backup
CronJob's restore path, `terraform apply`, and the `deploy` job end to end are
all written against no live cluster. See
[ADR-0018](../docs/adr/0018-self-hosted-deployment-shape.md) and
`deploy/terraform/README.md`.
