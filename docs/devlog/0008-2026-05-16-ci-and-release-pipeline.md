# 0008 — 2026-05-16 — CI hygiene and an un-undoable-publish-proof release path

> Narrative source for the wiseowl.com session. Continues 0007.

With both packages reviewed and ready, the remaining risk wasn't the code
— it was the *publish*. A NuGet version is immutable and permanent: you
can unlist it but never delete it and never re-upload that number. The
goal for this pass was a pipeline that makes a premature or accidental
push impossible while keeping a deliberate release a two-minute,
idempotent operation, and — separately — to stop CI firing on every
interim commit.

Three decisions were taken with the owner (each the safer option):

- **Trigger:** publishing fires only on *a GitHub Release being
  published*, not on a tag push. Creating the Release is itself the
  deliberate human act; a stray `git push --tags` cannot reach the
  workflow.
- **Auth:** NuGet.org **Trusted Publishing (OIDC)** — GitHub mints a
  short-lived token exchanged for a single-use key at run time. No
  long-lived NuGet secret lives in the repo at all (the strongest option,
  and it fits the project's general "least standing authority" instinct).
- **CI model:** feature-branch + PR. Work branches carry *no* trigger, so
  day-to-day commits never start a run; validation happens once at the PR
  and once at the merge.

`ci.yml` was rescoped: PR-into-`main` and push-to-`main` only,
`paths-ignore` for doc/asset-only changes (they can't break the build or
the API-doc-drift contract), and `concurrency: cancel-in-progress` so a
burst of pushes collapses to one run instead of a stale queue. Worth
recording plainly: `WiseOwl.Casc` is a *public* repo, so Actions minutes
are free and unlimited — the owner's "burning minutes" concern was, on
the facts, not a real cost. The hygiene was kept anyway: it keeps the run
history meaningful and is correct if the repo ever goes private. Not
inflating a non-problem is the same through-line as the rest of the
project.

`publish.yml` stacks four independent gates between a commit and an
immutable version: the release-only trigger; a `nuget` Environment with a
required reviewer (the run pauses for a human approval click); a
hard check that the release tag equals the committed
`Directory.Build.props` `<Version>` (you can't ship a number the repo
hasn't recorded); and `--skip-duplicate`, so any re-run is a no-op rather
than an error or overwrite. `docs/RELEASING.md` is the runbook: the
one-time GitHub Environment / `NUGET_USER` variable / nuget.org
Trusted-Publishing-policy setup, then the per-release checklist.

This infra commit goes straight to `main` as the bootstrap (the
branch/PR discipline it establishes starts with the *next* change). No
package has been published; the first real release is the owner's call
once the one-time setup in the runbook is done.
