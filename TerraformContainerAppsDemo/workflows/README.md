# Workflows

These two files are **not active**. They live here rather than in `.github/workflows/` so that this
samples repository never starts running Azure deployments. To use them, copy both into
`.github/workflows/` of the repository that owns your infrastructure and adjust the
`working-directory` and `paths` values to wherever your root module lives.

| File | What it does |
|------|--------------|
| `terraform.yml` | Plan on every pull request with the plan posted as a PR comment, then apply on `main` behind the `prod` environment gate. OIDC auth, no secrets in the repo. |
| `terraform-drift.yml` | Scheduled `terraform plan -detailed-exitcode` on weekday mornings. Opens or updates a `drift`-labelled issue on exit code 2 and closes it again on exit code 0. |

## What you have to set up first

**Repository variables** (Settings, Secrets and variables, Actions, Variables tab):
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

**Repository secret:** `ORDERS_API_KEY`, the placeholder downstream credential the root module
writes into Key Vault.

**A `prod` environment** in Settings with the reviewers you want. The `environment: prod` key on the
apply job is the entire approval gate; nothing in the YAML configures who approves.

**Three federated credentials** on the user-assigned identity or app registration, because the
subject changes per trigger:

```text
repo:OWNER/REPO:pull_request
repo:OWNER/REPO:ref:refs/heads/main
repo:OWNER/REPO:environment:prod
```

Two things to get right here:

1. The segment key is singular `ref:`, not `refs:`. HashiCorp's azurerm OIDC guide prints
   `refs:refs/heads/main`; GitHub mints the token and GitHub uses `ref:`.
2. Repositories created after 15 July 2026 use an immutable subject format that carries the owner
   and repository IDs: `repo:OWNER@OWNER-ID/REPO@REPO-ID:ref:refs/heads/BRANCH`. The IDs cannot be
   stripped with `include_claim_keys`. Read the real `sub` out of a token rather than copying a
   string from a tutorial written before that date.

Adding an `environment:` key to a job silently rewrites that job's subject, and the resulting
failure says nothing about environments. The precedence is: environment name if the job references
an environment, otherwise `pull_request` if the trigger was a pull request, otherwise the branch.

**State storage.** A storage account and a `tfstate` container matching
`infra/envs/<env>.backend.hcl`, with the identity granted Storage Blob Data **Contributor** scoped
to the container. Reader is not enough: `terraform plan` takes the lock, and taking the lock writes
blob metadata.

## The lock

The azurerm backend acquires an infinite blob lease on the state file. Nothing expires it. A runner
hard-killed mid-apply leaves the state locked until someone runs `terraform force-unlock <LOCK_ID>`
or breaks the lease in the portal. That is why `cancel-in-progress` is `false` and every plan and
apply passes `-lock-timeout=5m`.
