# Deploy runbook: first real deployment

The single ordered path from "I have a Hetzner account" to "the app answers
HTTPS on a real domain." `deploy/terraform/README.md` and `deploy/README.md`
cover each piece in depth; this is the sequence that ties them together.

Nothing here has run against a real VPS yet (see
[ADR-0018](../docs/adr/0018-self-hosted-deployment-shape.md)). Expect at
least one step below to need a fix on first use, and fix it in place rather
than treating the gap as a reason to start over.

## 1. Provision the server

```bash
cd deploy/terraform
cp terraform.tfvars.example terraform.tfvars   # fill in real tokens, CIDR, hostname
terraform init
terraform plan
terraform apply
```

Wait for cloud-init to finish before moving on: `ssh root@<server_ipv4> "cloud-init status --wait"`.

## 2. Make the container images pullable

`ghcr.io/tthanhtung92/finmy` and `ghcr.io/tthanhtung92/finmy/migrator` are
private by default on a private GHCR namespace. Either make both packages
public (Package settings on GitHub, simplest for a public repo's own image),
or add an `imagePullSecret` to the chart and reference it from
`api-deployment.yaml` and `migrate-job.yaml` (not wired up today, since no
decision was needed until a real cluster exists to pull into).

## 3. Get a kubeconfig Actions can use

```bash
ssh root@<server_ipv4> cat /etc/rancher/k3s/k3s.yaml > /tmp/kubeconfig
```

Edit `/tmp/kubeconfig`: change `server: https://127.0.0.1:6443` to
`server: https://<server_ipv4_or_fqdn>:6443`. Then:

```bash
gh secret set KUBE_CONFIG --repo tthanhtung92/finmy < <(base64 -w0 /tmp/kubeconfig)
rm /tmp/kubeconfig   # do not leave a live cluster credential on disk
```

## 4. Confirm SOPS_AGE_KEY is set

Already done this session:

```bash
gh secret list --repo tthanhtung92/finmy   # SOPS_AGE_KEY should be listed
```

If it needs rotating later, `deploy/README.md`'s "Generating a new key pair"
section covers it.

## 5. Point the chart's Ingress at the real host

`deploy/helm/finmy/values.yaml`'s `ingress.host` still says
`finmy.example.com`. Either edit it to the real hostname from
`terraform.tfvars`'s `hostname` and commit that change, or override it at
deploy time with `--set ingress.host=<real-host>` (the `deploy` job in
`release.yml` would need that flag added if going this route).

## 6. Turn the deploy job on

```bash
gh variable set DEPLOY_ENABLED --repo tthanhtung92/finmy --body true
```

Until this is set, tagging a release only publishes images; the `deploy`
job stays a no-op (see ADR-0018 for why).

## 7. Deploy

```bash
git tag v0.1.0
git push origin v0.1.0
gh run watch --repo tthanhtung92/finmy   # follow the deploy job
```

## 8. Verify

```bash
curl -sI https://<real-host>/health/live
curl -sI https://<real-host>/health/ready
```

Both should answer `200`. If `/health/ready` is not `200` yet, check the
migrate Job first (`kubectl get jobs`, `kubectl logs job/finmy-migrate-<N>`):
a pending migration is the most likely reason a fresh cluster's first deploy
sits at not-Ready.

## If it does not work

The gap is almost certainly one of the things ADR-0018 already named as
unverified: the migrate Job's retry budget against a cold `local-path` PVC,
cert-manager's CRDs not yet existing when the `ClusterIssuer` manifest
applies, or a Let's Encrypt HTTP-01 challenge failing against a DNS record
that just went live. Check `kubectl get certificate`, `kubectl describe
clusterissuer letsencrypt-prod`, and the `cert-manager` namespace's pod logs
before assuming the chart itself is wrong.
