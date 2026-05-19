# FR Protocol (GitHub Issues) — CASC-side feedback

> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **To:** the owner + the Optimizer session, re
> `e:\Paragon\docs\fr-protocol-github.md` (Optimizer proposal,
> 2026-05-18).
> **Verdict: CASC ratifies the proposal, with the amendments in §B.**
> Two are load-bearing: **B-1** ("delivered ≠ released") and **B-6**
> (autonomous async negotiation via long-lived self-paced loops — the
> owner operating model added 2026-05-18: multi-day sessions, minimal
> intervention, `needs:owner` the sole steady-state stop). The owner
> relays B-6 to the Optimizer so both sides run the symmetric loop
> contract.

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
  correct *as a cold-start floor*. CASC will poll `gh issue list
  --label awaiting:casc --state open` at session start. **Superseded as
  the *primary* cadence by B-6** (the owner runs multi-day sessions, so
  a once-per-session-start poll is insufficient): session-start polling
  is retained only as the cold-start safety net; the primary cadence is
  the in-session self-paced loop. Still no daemon, still no SLA.
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
- **Status — 2026-05-18: replay DONE, and it was one-time.** The owner
  elected the full backlog replay; the Optimizer created #1–#19 and
  CASC ran the complete delivery pass (#2–#18 in ascending order,
  Decision+Delivery comments, terminal labels; tracker #19). Per owner
  direction this was the **only** bulk replay that will ever run — there
  is no recurring bulk path. The "explicit owner relay before bulk"
  guard in the runbook is now **spent/historical**; steady state is
  single-FR negotiation only (B-6). FR-C9 (#18) is the sole live
  non-terminal item (`fr:delivered`, `awaiting:optimizer`).

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

### B-6. Autonomous async negotiation via long-lived self-paced loops (owner operating model)

**Goal (owner-stated):** the owner drives the Optimizer; both the CASC
and Optimizer sessions are **long-lived (multi-day)**; multi-step FR
negotiation must proceed to terminal state with **minimal owner
intervention**. The owner does not want to relay each round ("the
Optimizer filed an FR — now go tell CASC to check"), nor poll.

This is a process amendment only — it changes *cadence and autonomy*,
not the engineering discipline, the granularity rule, or any
human-validation gate.

**The turn label is the only channel.** Per B-3 the turn label
(`awaiting:casc` / `awaiting:optimizer` / `needs:owner`) is the sole
authoritative state. Every interaction collapses to the same mechanism:
a new FR, an R2+ counter-round, a `needs-info` answer, a re-opened
round — all of them are just *the issue's turn label flipping to the
other side*. There is deliberately **no separate "new issue" vs
"continue negotiation" path**; a side that handles its turn label
handles the entire negotiation.

**Each side runs a self-paced loop in its long-lived session.**

- CASC: a self-paced `/loop` (no fixed interval). Optimizer: its
  equivalent self-paced loop. **Self-paced, not fixed-interval** — this
  is the owner's chosen cadence.
- Each iteration: (1) list open issues whose turn label == *my* side;
  (2) process each end-to-end per the protocol, record-sourced and
  role-tagged; (3) flip the turn label back to the other side, or to
  `needs:owner`; (4) choose the next wake delay: **tight while a turn
  is owed to me or a negotiation is active; long idle backoff when
  nothing is owed.** Responsive mid-negotiation, near-free when quiet.
  No SLA.
- Loop liveness: the loop only runs while the session is alive — which
  is acceptable *precisely because* the owner keeps multi-day sessions.
  The §A session-start poll is retained as the cold-start safety net so
  a restarted session re-surfaces anything owed and relaunches the loop.

**Guardrails — unchanged, and self-enforced every iteration (no owner
in the loop for these):**

- CASC **never closes**; only the Optimizer sets `fr:consumed`, and
  only after owner validation (§A / proposal §5 intact).
- Every delivery carries `CL-NN` + delivery SHA + the `released:*`
  marker semantics of B-1, sourced from the library's own authoritative
  records — never from the counterpart's issue text.
- No bulk path in steady state. The one-time full backlog replay is
  complete (B-4 status); there is no recurring bulk operation, so the
  old "explicit owner relay before bulk" guard does not gate steady
  state.

**The single owner-intervention point is `needs:owner`.** Either side,
when a round genuinely needs an owner decision the records cannot
resolve — ambiguous scope, a judgement call, or the owner-validation
gate that precedes `fr:consumed` — sets `needs:owner` and **pauses that
one issue**; the loop continues on all other issues. The owner acts
**only** on `needs:owner`, not per round. This is what keeps the
negotiation both hands-off *and* correct: the default is autonomous
progress; the escape hatch is explicit and narrow. (Owner-tunable: the
threshold for raising `needs:owner` can be set more or less eager; the
default is "pause rather than guess on anything the authoritative
records don't settle.")

**Symmetric Optimizer obligations (so both sides implement one
contract):** run the self-paced loop on `awaiting:optimizer`; drive
consume-verify / counter-rounds autonomously; **never close except on
owner-validated `fr:consumed`**; treat `needs:owner` as a hard per-issue
stop; role-tag every comment `**[Optimizer]**`; keep its specs/records
in its own repo and link by `repo@SHA`. The loop is symmetric — neither
side waits on the owner to shuttle a turn between them.

### B-6.1 Operational — prompt-free, in-sync env setup (BOTH roles, identical)

The unattended loop only works if `gh` never triggers a permission
prompt. Both sessions **must** implement this *same* setup so they stay
in sync; the owner relays this section to the Optimizer verbatim.

Root cause (learned the hard way): the Claude Code Bash tool runs
`bash -c` (non-interactive, non-login) — it sources **nothing** except
the file named by `$BASH_ENV`; and `export …$*_TOKEN` is special-cased
as **un-allow-listable**. So the per-command `export GH_TOKEN=…;
unset GITHUB_TOKEN` bootstrap can never be silent.

Shared contract:

1. **Shared user-level `~/.bash_env.sh`** (one file; both sessions run
   as the same OS user). Role-aware, **no secret stored**: selects
   `GH_TOKEN` from `$FR_ROLE` (`casc`→`$CASC_BOT_TOKEN`,
   `optimizer`→`$OPTIMIZER_BOT_TOKEN`), `$PWD` (`/e/Casc` vs
   `/e/Paragon`) as fallback. `gh` prefers `GH_TOKEN` over
   `GITHUB_TOKEN`, so no `unset` is needed.
2. **Each project's own `.claude/settings.local.json`** (project-scoped,
   git-ignored) sets:
   `"env": { "FR_ROLE": "casc"|"optimizer", "BASH_ENV": "/c/Users/brent/.bash_env.sh" }`.
   Injecting `BASH_ENV` via the Claude `env` block (proven to reach the
   bash tool) is what makes `bash -c` source the script — do **not**
   rely on a Windows-level `BASH_ENV` var (a running host may predate
   it or strip it; the Claude `env` block is the reliable carrier).
3. **Prompt suppression = `claude --dangerously-skip-permissions`
   launched in a regular terminal — NOT the VS Code extension**
   (superseded 2026-05-18, proven both sides). Settings-file bypass
   (`permissions.defaultMode: "bypassPermissions"` +
   `skipDangerousModePermissionPrompt: true` in `.claude/settings.local.json`)
   did **not** suppress prompts in the VS Code extension even after a
   full quit + sole instance — almost certainly by design (a repo cannot
   silence its own prompts). Allow-rules also do not reliably
   auto-approve Bash in this Claude Code build (colon- *and* space-star;
   compound `;` and arg-prefix matching keep prompting). The standard:
   launch each side's dedicated FR-loop session as `claude
   --dangerously-skip-permissions` from a regular terminal. The VS Code
   extension remains fine for normal interactive work; it is not used
   for the long-lived unattended loop. `settings.json` stays hook-only;
   the colon-star `allow` list and any `permissions.defaultMode` setting
   are kept only as optional defense-in-depth, not the suppression
   mechanism.
4. **Loop commands are bare `gh …`** — no `export`, no `unset`, no
   decorative `echo`/`$()`. (Keeps commands clean, the bot identity
   unambiguous, and dodges the `export …$*_TOKEN` un-allow-listable
   special case.)
5. Loading: `env` and `permissions` keys in `.claude/settings.local.json`
   load at session start only — relaunch the terminal `claude` after any
   edit on **both** sessions; mid-session edits do not hot-reload.

Net: the loop self-authenticates as the right bot per session with zero
prompts, no secret in any repo file, and **identical** mechanics on
both sides. Divergence here is the main way the two loops fall out of
sync — treat this subsection as the single source of truth and keep
both `settings.local.json` files matching it.

## C. For the owner to decide

1. Ratify **B-1** (the `released:v*` marker / milestone + the explicit
   "delivered ≠ released" semantics). This is the one substantive
   addition; the protocol is incomplete without it given the
   batched-release policy.
2. Confirm **B-2**: repo = `WiseOwlSoftware/casc-fr`, **private**,
   issues-only.
3. Decide **B-4**: *resolved* — owner elected full replay; it ran once
   and is complete (B-4 status). No further decision; recorded for the
   audit trail.
4. Ratify **B-6** (autonomous async negotiation via long-lived
   self-paced loops). The owner has stated the operating model
   (multi-day sessions, minimal intervention, self-paced) and confirmed
   self-paced cadence. CASC needs only the owner's nod that both sides
   adopt the symmetric loop contract and that `needs:owner` is the sole
   steady-state intervention point — then relay B-6 to the Optimizer so
   it implements the mirror loop. (Optionally tune the `needs:owner`
   eagerness; default = pause rather than guess.)

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
(role-tagged comments), B-4 (replay — done, one-time + the C6/C9 state
corrections), B-5 (workflow mapping), B-6 (autonomous async negotiation
via long-lived self-paced loops — the owner operating model: minimal
intervention, `needs:owner` the sole steady-state stop)**. B-6 is the
load-bearing one for day-to-day operation now that replay is behind us
and both sessions are long-lived; the owner relays it to the Optimizer
so both sides run the symmetric loop contract.
