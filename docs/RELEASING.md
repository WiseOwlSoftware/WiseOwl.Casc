# Releasing WiseOwl.Casc

How CI and publishing work, the one-time setup to do **before the first
release**, and the per-release checklist.

A published NuGet version is **immutable and permanent** — it can be
unlisted but never deleted and never re-uploaded under the same number.
Everything below exists to make an accidental or premature publish
impossible, and a deliberate one a two-minute, idempotent operation.

---

## How the pipeline behaves

### CI (`.github/workflows/ci.yml`) — validation only

| Event | CI runs? |
|---|---|
| Push to a work/feature branch | **No** — no trigger matches; commit freely |
| Pull request into `main` | Yes |
| Push/merge to `main` | Yes |
| PR/push that touches **only** `**/*.md`, `docs/**`, `assets/**`, `.gitignore`, `.gitattributes` | **No** — cannot affect the build or the API-doc contract |
| Several pushes in quick succession on the same ref | Only the newest runs — `concurrency: cancel-in-progress` cancels superseded runs |

Day-to-day work happens on branches and never starts a run. Validation
happens once, at the integration point (the PR and the merge). It builds
+ tests on Windows and enforces the `docs/api` no-drift contract on Linux.

> `WiseOwl.Casc` is a **public** repo, so Actions minutes are free and
> unlimited — but the model above keeps the run history meaningful and
> would still be correct if the repo ever went private.

### Publish (`.github/workflows/publish.yml`) — gated, idempotent

The only trigger is **a GitHub Release being published**. Four gates:

1. **Trigger** — only `release: published`. No branch push, tag push, or
   interim commit can reach this workflow.
2. **Environment approval** — the job runs in the `nuget` Environment,
   which has a required reviewer. The run pauses for a human approval
   click before anything is packed or pushed.
3. **Version guard** — the release tag (`vX.Y.Z[-suffix]`) must equal the
   committed `Directory.Build.props` `<Version>`, or the job fails before
   packing. You cannot ship a number the repo hasn't recorded.
4. **Idempotent push** — `dotnet nuget push --skip-duplicate`: a re-run
   of an already-published version is a no-op, never an overwrite.

Auth is **NuGet.org Trusted Publishing (OIDC)** — GitHub mints a
short-lived token at run time and exchanges it for a single-use API key.
**No long-lived NuGet secret is stored in this repository.**

---

## One-time setup (do this before the first release)

> **All GitHub steps below are in the _repository_ settings, NOT the
> organization settings.** Go to
> `https://github.com/WiseOwlSoftware/WiseOwl.Casc` and click the
> **Settings** tab on the repo's own tab bar (Code · Issues · Pull
> requests · … · **Settings**). The org-level Settings page has no
> Environments / Actions-variables section — that is expected; those are
> per-repository. Only step 3 (Trusted Publishing) is done on nuget.org.

### 1. GitHub: create the `nuget` Environment

**Repository** Settings (see note above) → left sidebar, **Code and
automation** section → **Environments** → **New environment** → name it
exactly `nuget`.

(Environment protection rules are free for **public** repos, which this
is — no paid plan needed.)

- Under **Deployment protection rules**, enable **Required reviewers** and
  add yourself (Brent Rector). This is the manual approval gate. Click
  the **Save protection rules** button under that section (separate from
  any other save on the page).
- **Deployment branches and tags — this MUST allow the release tag or the
  publish job fails instantly at the gate, before approval.** `publish.yml`
  triggers on a GitHub Release, which runs on the *tag* ref
  (`refs/tags/v0.1.0-alpha`), **never** on `main`. So:
  - Select **Selected branches and tags**.
  - **Add deployment branch or tag rule** → ref type **Tag** → pattern
    **`v*`**.
  - A `main` *branch* rule is unnecessary here (this environment is only
    ever entered by the release-triggered, tag-ref publish) and, if it is
    the *only* rule, it actively blocks every release. Either delete it
    or leave it alongside the `v*` tag rule — but the `v*` tag rule must
    exist.

  Symptom if this is wrong: the run ends in ~1 s with
  *"Tag "v…" is not allowed to deploy to nuget due to environment
  protection rules"* — a safe failure (nothing is published), fixed by
  adding the `v*` tag rule and re-running.

### 2. GitHub: add `NUGET_USER`

The workflow reads `${{ secrets.NUGET_USER }}` from a secret scoped to
the **`nuget` environment**. Repo → **Settings → Environments → nuget →
Environment secrets → Add secret**:

- Name: `NUGET_USER`
- Value: **exactly the "Package owner" shown on the Trusted Publishing
  policy** (step 3) — for this project the `WiseOwlSoftware`
  organization, so the value is **`WiseOwlSoftware`**.

This is the single most error-prone value. The `NuGet/login` `user:`
input must identify the nuget.org account whose Trusted Publishing
policy is being matched and *as which* packages are published. Because
the policy's Package owner is the **organization** `WiseOwlSoftware`
(packages live under the reserved `WiseOwl.*` prefix owned by that org),
the value is the **org account name**, *not* the individual maintainer's
personal handle. It would be the personal handle only if the policy's
Package owner were set to the individual account instead.

(A username is not actually sensitive, so a repo/environment *variable*
would work too — but it is stored here as an environment secret and the
workflow reads `secrets.NUGET_USER` accordingly. `secrets.*` resolves
because the publish job targets `environment: nuget`. No API key is
stored anywhere — auth is OIDC.)

### 3. NuGet.org: register the Trusted Publishing policy

nuget.org → your account (or organization) → **Trusted Publishing** →
add **one** policy:

| Field (label may vary) | Value |
|---|---|
| Package owner / username | the `NUGET_USER` account/org that holds the reserved `WiseOwl.*` prefix |
| Repository owner | `WiseOwlSoftware` |
| Repository | `WiseOwl.Casc` |
| Workflow file | `publish.yml` |
| Environment | `nuget` |

**One policy is sufficient and correct.** A Trusted Publishing policy is
scoped to a *repository + workflow (+ environment)* — there is **no
package-id field**. This single policy authorizes the `publish.yml`
workflow to push *every* package it builds, so it covers **both**
`WiseOwl.Casc` and `WiseOwl.Casc.Diablo4` (both are packed and pushed by
that one workflow). Because the owning account holds the reserved
`WiseOwl.*` prefix, the workflow may **create both new package ids on the
first publish** — they need not pre-exist. The policy binds "publish as
this account" to *exactly* this repo + workflow + environment and nothing
else.

### 4. (Optional — does not affect publish safety) Branch protection on `main`

This step only enforces the feature-branch/PR habit as a rule. The
irreversible-publish protection (release-only trigger, environment
approval, version guard, `--skip-duplicate`) is **independent** of branch
protection — skip this entirely if the UI fights you and nothing about
publishing becomes less safe.

Repo → **Settings → Branches → Add rule** for branch name pattern
`main`. (GitHub has two UIs: the **classic Branch protection rule**
screen described here, and the newer **Rulesets** screen. They differ —
the classic screen lists required checks with **no per-check source
dropdown**; the Rulesets screen has a source dropdown you should set to
**GitHub Actions**. Either is fine; the classic screen is simpler.)

Tuned for a **solo owner** — the real trap is locking yourself out:

- ☑ **Require a pull request before merging**
  - ☐ **Require approvals — leave OFF.** With one maintainer, requiring
    ≥1 approval means you cannot approve your own PR and can never merge.
    With approvals off, GitHub still allows self-merge after CI passes.
- ☑ **Require status checks to pass before merging**
  - ☑ **Require branches to be up to date before merging** (recommended)
  - Add the two required checks. The control is a **search box**, not a
    pre-filled list ("No required checks" is just the empty selected-list
    state, not an error). Type each exact job name and select it:
    - `Build & test`
    - `API docs in sync`

    These are the `name:` values of the two jobs in `ci.yml`; the `&`
    and spaces are part of the string. The search only finds checks
    GitHub observed in the last ~week — CI runs on every push/PR to
    `main`, so they are present once CI has run once (it has).
- Optional, clean history: ☑ **Require linear history** (then merge via
  Squash or Rebase). ☐ Conversation resolution — fine either way.
- Leave **OFF**: Require signed commits (blocks unsigned commits unless
  you already sign); **Require deployments to succeed** (do NOT add the
  `nuget` environment — it gates *publishing on release*, not merging);
  Merge queue; Lock branch; Restrict who can push. Keep **Allow force
  pushes** and **Allow deletions** unchecked.
- **"Do not allow bypassing the above settings"** — judgment call.
  Checked = even you must PR every change to `main`. Unchecked = you keep
  an admin escape hatch. Recommended **unchecked** for a solo owner: the
  irreversible risk (publish) is gated independently by `publish.yml`, so
  strictness here is hygiene, not safety.

---

## Per-release checklist

1. **Bump the version.** Edit `<Version>` in `Directory.Build.props`
   (e.g. `0.1.0-alpha` → `0.1.0` or `0.2.0-alpha`). NuGet treats any
   `-suffix` as a pre-release automatically.
2. **Update `CHANGELOG.md`.** Replace/extend the top entry with the new
   version and date.
3. **Land it on `main`** via the normal branch → PR → green CI → merge
   flow. The released commit must be on `main`.
4. **Tag & create the GitHub Release** from that commit. The tag must be
   `v<Version>` — exactly matching step 1:

   ```sh
   gh release create v0.1.0 \
     --target main \
     --title "v0.1.0" \
     --notes-file - <<'NOTES'
   See CHANGELOG.md.
   NOTES
   ```

   (For a pre-release, add `--prerelease`.)
5. **Approve the deployment.** The `Publish` workflow starts and pauses
   on the `nuget` environment — open the run, click **Review
   deployments → Approve**.
6. **Verify.** The job packs, runs tests, OIDC-logs-in, and pushes both
   `.nupkg` (symbols `.snupkg` ride along automatically). Confirm both
   packages appear on nuget.org at the new version.

### If something goes wrong

- **Tag/version mismatch** → the job fails *before* packing with a clear
  error. Fix `<Version>` (or delete and re-create the release/tag
  correctly) and re-release. Nothing was published.
- **Need to re-run** → safe. `--skip-duplicate` makes an
  already-published version a no-op; an unpublished one proceeds.
- **Published a bad version** → it cannot be deleted. *Unlist* it on
  nuget.org and publish a corrected higher version. This is why the gates
  exist — prevention, not cure.
