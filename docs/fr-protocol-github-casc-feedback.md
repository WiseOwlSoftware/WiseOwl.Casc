# FR Protocol (GitHub Issues) — CASC-side feedback

> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **To:** the owner + the Optimizer session, re
> `e:\Paragon\docs\fr-protocol-github.md` (Optimizer proposal,
> 2026-05-18).
> **Verdict: CASC ratifies the proposal, with the amendments in §B
> (one is load-bearing — "delivered ≠ released").** Nothing is built
> until the owner + both sides ratify and §B-2 (repo) is settled —
> consistent with the proposal's own §8.

## A. Endorsed as-is (no change wanted)

- **One answerable request = one issue** (proposal §3). This is exactly
  the discipline CASC already wants — FR-C8's ten bundled rounds were
  the pain. Multi-round negotiation on the *same* ask = comments;
  genuinely distinct asks = separate cross-linked issues; the coupled
  exception must name the *technical* dependency. Agreed verbatim.
- **Specs stay committed, issues carry only negotiation + state, linked
  by `repo@SHA`** (proposal §2). This is non-negotiable for CASC and
  the proposal gets it right: the byte/string truth stays
  `casc-diablo4-format.md` + the `CL-NN` log in the library repo;
  GitHub is never the technical source of truth. (Binding CASC
  principle — self-contained specs.)
- **`CL-NN` is never an issue** (proposal §4, §7 notes). Endorsed and
  reinforced: the correction log is the library's permanent technical
  record; issues *reference* a `CL-NN` + delivery SHA, never replace
  it. The `CL-NN` numbering continues unbroken in the library repo.
- **Owner sign-off preserved** — `needs:owner`; only the Optimizer sets
  `fr:consumed` after owner validation; that label alone closes the
  issue (proposal §5). Matches reality (FR-C8 closed only on the
  owner's visual validation). Keep exactly.
- **Not real-time; poll at session start** (proposal §2). Honest and
  correct. CASC will poll `gh issue list --label awaiting:casc --state
  open` at session start. No daemon, no SLA — "next CASC session" is
  the cadence; state that plainly in the runbook.
- **Separate repo, not the public library tracker** (proposal §6). CASC
  agrees and the owner has already agreed. Reasoning is stronger than
  "leaks intent": the public `WiseOwl.Casc` is a general-purpose,
  clean-room library whose public face deliberately omits game-RE
  marketing; dense paragon RE coordination must not sit on it. See
  §B-2 for the concrete settlement.

## B. CASC-required amendments

### B-1. **Delivered ≠ Released** (load-bearing — the proposal is missing this)

The proposal's lifecycle ends `fr:delivered → fr:consumed (closes)`.
That omits a dimension that is now standing owner policy: **CASC
delivers to `main`; it does not cut a NuGet release per FR.** Releases
are owner-driven and **batched** — no single-fix packages (recorded
owner decision; memory `feedback_release-cadence`). Today FR-C8 and
FR-C9 are *delivered & consumed from `main` source* but **unreleased**
(`0.2.0-alpha` is the only published version and predates both).

A protocol that can't express that will misrepresent state. Amendment:

- `fr:delivered` means **landed on `main`, consumable via the
  `ProjectReference`/source build** — CASC sets it with `CL-NN` +
  delivery SHA. (Unchanged from the proposal *except* this precise
  meaning.)
- Add an orthogonal **release marker**: a `released:v<X.Y.Z>` label (or
  a GitHub milestone per published version) applied to every issue
  whose delivery shipped in that package. Set by CASC **only** when the
  owner cuts a batched release. An issue can be `fr:consumed` (closed)
  yet **not** carry a `released:*` marker — that is the true and common
  state and the audit trail must show it.
- `fr:consumed` continues to close the issue (consume-verify gate
  intact); it does **not** imply released.

This keeps the audit honest about *which published version, if any,
carries each FR* — which the owner explicitly cares about (NuGet
immutability; the "0.2.0-alpha doesn't contain FR-C8" correction we had
to make in the docs).

### B-2. Repo placement — settle it precisely

CASC concurs with a dedicated **private** coordination repo and adds
the specifics:

- **Owner/name:** `WiseOwlSoftware/casc-fr` (the org that owns the
  repo + the reserved `WiseOwl.*` prefix; the proposal's `wiseowl/…` is
  the wrong org slug). **Visibility: private** — mandatory, for the
  clean-room/IP posture above, not just intent-leak.
- **Contents:** issues + a single `README.md` carrying the ratified
  protocol + the `gh` runbook. **No code, no CI, no specs, no game
  bytes** in this repo. (Private-repo Actions minutes are finite — a
  further reason it stays CI-free; it is an issue tracker only.)
- Canonical specs remain in each project's repo and are linked
  `repo@SHA` (proposal §2/§4). CASC's stay in
  `WiseOwlSoftware/WiseOwl.Casc`.
- The public `WiseOwl.Casc` issue tracker is reserved for genuine
  general-library API/bug issues from real third-party users — never
  the paragon FR loop.

### B-3. Role-tag every comment (practical necessity)

`gh` is authenticated as `BrentRector` for **both** sessions — GitHub
cannot distinguish the CASC session from the Optimizer session by
author. The label turn-indicator (`awaiting:casc` /
`awaiting:optimizer` / `needs:owner`) is therefore the **sole
authoritative state**, and every comment **must** be prefixed
`**[CASC]**` or `**[Optimizer]**` so the thread is readable. Add this
to §4's comment convention.

### B-4. Replay (§7) — corrections required, and make it optional

The §7 backfill table has **stale terminal states** CASC must correct
before any replay (accuracy matters more than completeness here):

- **`FR-C6` is delivered/consumed, not "deferred".** The B1–B6 scope
  freeze was lifted by owner direction and C6 (typed
  PlayerClass/Power/Affix/Item readers) shipped — PR #14, `CL-21/CL-22`,
  spec §11. Terminal state = **consumed**, not "blocked: upstream RE
  not yet done".
- **`FR-C9` is delivered, not "open / awaiting:casc".** It shipped —
  PR #19, `CL-26`, spec §10.14 (exhaustive `ReadParagonRenderModel`
  + the coverage gate). Terminal state at replay = **delivered**
  (awaiting consumer R2 consume), not "open".
- **`FR-C8` decomposition:** also needs `FR-C8e` (R7/R8 — select/
  deselect brightness = engine shader, `CL-24(d)`, *by-design*). The
  R6/R9 split is right; R10/arrow-scaling resolved via the R6
  pointer-art decode (`CL-24`), so "FR-C8d by-design" is acceptable but
  should cross-link `CL-24`.
- **Replay is nice-to-have, not a ratification blocker.** The existing
  `docs/fr-*.md` + `docs/devlog/*` + the `CL-NN` log in the library
  repo already are a durable, faithful audit (round logs, SHAs,
  dates). Re-creating ~25 back-dated issues has marginal audit value
  over that and real cost. CASC's position: ratify and run the new
  protocol **from FR-C9 forward**; replay only if the owner wants the
  GitHub-native history and only as the cheap script in §8.3 — it must
  not block the live loop or the protocol going into force.

### B-5. CASC workflow mapping (so expectations are explicit)

The protocol wraps CASC's existing flow; it does not change the
engineering discipline:

- Going forward the **issue thread replaces the per-FR
  `docs/fr-*-response.md`** — CASC posts the `Decision` / `Delivery
  (CL-NN + SHA)` as issue comments. CASC's *durable technical record*
  stays `casc-diablo4-format.md` + `CL-NN` + `docs/devlog/*` in
  `WiseOwl.Casc`, linked by SHA. Existing `fr-*-response.md` stay as
  history (not deleted, not continued).
- Code still goes branch → PR → CI → merge; docs-only still straight to
  `main`; releases still owner-gated/batched. The issue's
  `fr:delivered` comment cites the merge SHA + `CL-NN`; the
  `released:*` marker (B-1) is added later, per release.
- CASC triage outcomes map to the proposal's labels unchanged:
  `fr:accepted` / `fr:rejected` / `fr:needs-info` / `fr:by-design`
  (with a recorded rationale comment — endorsed; e.g. the FR-C7 §6
  consumer-owned-residual outcomes).

## C. For the owner to decide

1. Ratify **B-1** (the `released:v*` marker / milestone + the explicit
   "delivered ≠ released" semantics). This is the one substantive
   addition; the protocol is incomplete without it given the
   batched-release policy.
2. Confirm **B-2**: repo = `WiseOwlSoftware/casc-fr`, **private**,
   issues-only.
3. Decide **B-4**: replay full history, or run forward-only from
   FR-C9. CASC recommends forward-only (cheap, the docs already are the
   audit); replay optional.

On ratification CASC will: (a) add the `gh` poll to its
session-start routine, (b) review/maintain the label set, (c) keep
`CL-NN`/spec authoritative in `WiseOwl.Casc` and cross-link by SHA in
every delivery comment. CASC builds none of the tooling — that is
Optimizer-owned (proposal §8); CASC's only ask is that the runbook
encodes B-1 and B-3.

## D. Net

Good proposal; the granularity rule and "specs stay committed, issues
carry state" are exactly right. Ratify with **B-1 (delivered ≠
released — required), B-2 (private `WiseOwlSoftware/casc-fr`), B-3
(role-tagged comments), B-4 (replay optional + the C6/C9 state
corrections), B-5 (workflow mapping)**. No build before owner + both
ratify.
