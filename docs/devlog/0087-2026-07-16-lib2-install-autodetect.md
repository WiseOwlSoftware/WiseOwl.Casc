# 0087 — LIB-2: auto-detect the Diablo IV install location

**Date:** 2026-07-16
**Work item:** casc-fr#44 (LIB-2 — comprehensive-data-exposure program)
**CL:** CL-91 · `Diablo4Storage.Open()` / `TryLocateInstall`

`Diablo4Storage.Open` required an explicit `installPath` (the recon/test
harness hardcoded `D:\Diablo IV` or read `WISEOWL_CASC_INSTALL`). LIB-2 adds
auto-detection so a consumer can just call `Open()`.

## Source

The install path is in the Windows registry — the Battle.net uninstall entry:

```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Diablo IV
    InstallLocation    REG_SZ    D:\Diablo IV
```

**Gotcha:** Battle.net's uninstaller is 32-bit, so the key lands under
**`WOW6432Node`**, not the 64-bit view — a native-view `reg query` misses it.
The locator tries both views.

## Shipped

- `Diablo4Storage.TryLocateInstall(out string? path)` — resolves the
  `WISEOWL_CASC_INSTALL` override first, then (on Windows) the registry; accepts
  a candidate only if it carries a `.build.info` (a real CASC install).
- `Diablo4Storage.Open()` / `OpenAsync()` — no-arg, open the auto-detected
  install, or throw a clear `CascException` ("set the env var or pass a path").
- `Diablo4Storage.InstallPathEnvironmentVariable` const.
- The explicit-path overloads stay for custom / non-Windows installs.

**Dependency-free:** the registry read shells out to `reg.exe` via `Process`
(guarded by `OperatingSystem.IsWindows()`) — no `Microsoft.Win32.Registry`
package added, keeping the package's zero-runtime-dependency profile. `reg`
invoked via `ProcessStartInfo.ArgumentList` (the space in "Diablo IV" is quoted
automatically). Test `LIB2_auto_detects_install_and_opens`; recon
`SnoScan locate`. Verified: auto-detected `D:\Diablo IV`, opened 862,224 SNOs.
