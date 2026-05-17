# 0009 — 2026-05-17 — First publish, and an analyzer-honesty correction

> Narrative source for the wiseowl.com session. Continues 0008.

Both packages are public. `WiseOwl.Casc` and `WiseOwl.Casc.Diablo4`
`0.1.0-alpha` (plus symbols) were pushed to nuget.org through the gated
`publish.yml` — release-triggered, environment-approved, OIDC, no stored
key. The first run proved every gate the hard way, exactly as designed:

1. **Deployment-policy gate.** The `nuget` environment's deployment
   branches/tags policy allowed only `main`; a Release runs on the *tag*
   ref, so the job died in ~1 s with "tag not allowed to deploy". A
   `v*` tag rule fixed it. Nothing was published.
2. **OIDC identity gate.** The token exchange 401'd: the `NuGet/login`
   `user:` must be the **policy creator's** individual account, *not*
   the policy's Package owner. Earlier guidance had flip-flopped to the
   org name; NuGet's own error stated the rule verbatim and settled it
   (`BrentRector`, the creator; packages still owned by the
   `WiseOwlSoftware` org — ownership and auth identity are independent).
   Still nothing published — the failure was pre-push.
3. **Approval gate.** Required-reviewer pause, approved, then build →
   test → pack → push. All four artifacts accepted; the Trusted
   Publishing policy is now permanently active.

Every failure was fail-safe (pre-push), which is the property the
pipeline was built for: getting the irreversible step wrong cost
nothing.

The honest beat of this entry: the post-publish annotations showed the
release built with **6 analyzer warnings** — and the project's
repeatedly-asserted "0 warnings" status (devlog 0004, memory, the
initial CHANGELOG) **was never true**. Reproduced locally: two distinct
findings, each ×3 TFMs — `CA1711` (`NodeAttribute` ends in "Attribute")
and `CA1822` (`Diablo4Storage.SnoPath` uses no instance state). CI ran
the analyzers all along; nothing failed on warnings and nobody counted,
so an unverified claim rode forward through every status summary. That
is precisely the "don't fake a pass" failure mode this project exists to
avoid, applied to itself. The record was corrected rather than quietly
patched: the CHANGELOG, memory, and this log all state that `0.1.0-alpha`
shipped with the 6 warnings.

`0.1.1-alpha` resolves them on the merits, not by silencing: `SnoPath`
became `static` (it is a pure path formatter — the cleaner API, a
breaking change taken now because pre-1.0 prerelease is the only cheap
time), and `NodeAttribute` kept its name under a *documented* `CA1711`
suppression — "Attribute" is the serialized `eAttribute` domain term and
the canonical byte-format spec, ARTICLE-SOURCE, and upstream RE record
all use it; renaming would diverge the code from its own specification.
A suppression with a real justification is honest; a rename to satisfy a
guideline at the cost of spec↔code coherence would not have been. CI
actions were also moved to the Node-24 majors ahead of the 2026-06-02
runner deprecation. Build is now genuinely analyzer-clean, verified by
rebuilding — the new standing rule being: never assert a clean-build
metric without rebuilding to confirm it.
